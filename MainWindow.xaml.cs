using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ImageAsynx;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WpfLab_2
{
    public partial class MainWindow : Window
    {

        private List<string> images_paths;
        private List<Image<Rgb24>> images;

        private CancellationTokenSource cancelTokenSource;
        private CancellationToken token;

        private bool calculations_status;

        ArcFace AF;

        public MainWindow()
        {
            InitializeComponent();

            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;

            images_paths = new List<string>();
            images = new List<Image<Rgb24>>();
            AF = new ArcFace();
            calculations_status = false;
        }

        private void Load()
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Filter = "Images (*.jpg, *.png)|*.jpg;*.png";
            var projectRootFolder = System.IO.Path.GetFullPath("../../../../Images");
            dialog.InitialDirectory = projectRootFolder;
            var response = dialog.ShowDialog();
            if (response == true)
            {
                foreach (var path in dialog.FileNames)
                {
                    var face = SixLabors.ImageSharp.Image.Load<Rgb24>(path);
                    images.Add(face);
                    images_paths.Add(path);
                }
            }
        }
        private void Grid_Construct()
        {
            int n = images_paths.Count;
            for (int i = 0; i < n + 1; i++)
            {
                table.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                table.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

                if (i > 0)
                {
                    var uri = new System.Uri(images_paths[i - 1]);
                    var bitmap = new BitmapImage(uri);

                    var image1 = new System.Windows.Controls.Image();
                    image1.Source = bitmap;
                    var image2 = new System.Windows.Controls.Image();
                    image2.Source = bitmap;

                    Grid.SetColumn(image1, 0);
                    Grid.SetRow(image1, i);
                    table.Children.Add(image1);

                    Grid.SetColumn(image2, i);
                    Grid.SetRow(image2, 0);
                    table.Children.Add(image2);
                }
            }
        }
        public void Gride_Clear()
        {
            calculations_status = false;
            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;

            int size = images.Count;
            if (size == 0)
            {
                return;
            }

            table.Children.Clear();
            progress_bar.Value = 0;
            for (int i = 0; i < size + 1; i++)
            {
                table.RowDefinitions.Clear();
                table.ColumnDefinitions.Clear();
            }

            images_paths.Clear();
            images.Clear();
        }

        private async Task EmbeddingsMatrixAsync(List<Image<Rgb24>> list_images, CancellationToken token, List<Task> tasks)
        {
            int step1 = 50 / list_images.Count;

            for (int i = 0; i < list_images.Count; i++)
            {
                try
                {
                    Task task1 = AF.GetEmbeddings(list_images[i], token);
                    progress_bar.Value += step1;
                    tasks.Add(task1);
                }
                catch (OperationCanceledException e1)
                {
                    Console.WriteLine($"{nameof(OperationCanceledException)} thrown with message: {e1.Message}");
                }
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException e2)
            {
                Console.WriteLine($"{nameof(OperationCanceledException)} thrown with message: {e2.Message}");
            }
        }
        private async Task DistSimilMatrixAsync(List<Image<Rgb24>> list_images, CancellationToken token)
        {
            int step2 = 50 / (list_images.Count * list_images.Count);

            for (int i = 0; i < list_images.Count; i++)
            {
                for (int j = 0; j < list_images.Count; j++)
                {
                    var cell_result = new Label();
                    Grid.SetColumn(cell_result, i + 1);
                    Grid.SetRow(cell_result, j + 1);
                    cell_result.HorizontalAlignment = HorizontalAlignment.Center;
                    cell_result.VerticalAlignment = VerticalAlignment.Center;
                    cell_result.FontSize = 12;

                    token.ThrowIfCancellationRequested();
                    
                    var dist = await AF.Distance(list_images[i], list_images[j], token);
                    var sim = await AF.Similarity(list_images[i], list_images[j], token);
                    cell_result.Content = $"Distance: {dist * dist}\n Similarity: {sim}";
                    progress_bar.Value += step2;
                    
                    table.Children.Add(cell_result);
                }
            }
            if (!token.IsCancellationRequested)
            {
                progress_bar.Value = 100;
            }
        }
        private async Task Start_calc_ClickAsync()
        {
            var tasks = new List<Task>();
            await EmbeddingsMatrixAsync(images, token, tasks);
            await DistSimilMatrixAsync(images, token);
            calculations_status = true;
        }

        private void Load_image_Click(object sender, RoutedEventArgs e)
        {
            Gride_Clear();
            Load();
            Grid_Construct();
        }
        private void Start_calc_Click(object sender, RoutedEventArgs e)
        {
            if (images.Count == 0)
            {
                MessageBox.Show("Load some images.");
                return;
            }
            else if (calculations_status)
            {
                MessageBox.Show("You've calculated this data. Choose new one.");
                return;
            }
            Task task = Start_calc_ClickAsync();
        }
        private void Clear_calc_Click(object sender, RoutedEventArgs e)
        {
            Gride_Clear();
        }
        private void Stop_calc_Click(object sender, RoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
        }

    }

}


