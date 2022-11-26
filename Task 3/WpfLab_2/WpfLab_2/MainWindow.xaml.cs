﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
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
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static System.Net.Mime.MediaTypeNames;

namespace WpfLab_2
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource cancelTokenSource;
        private CancellationToken token;
        private readonly List<Image<Rgb24>> list_images;
        private List<Tuple<byte[], string>> images_paths;
        private readonly ArcFace AF;

        public MainWindow()
        {
            InitializeComponent();

            cancelTokenSource = new();
            token = cancelTokenSource.Token;
            images_paths = new();
            list_images = new();
            AF = new();
        }

        private void Load()
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Filter = "Images (*.jpg, *.png)|*.jpg;*.png";
            string projectRootFolder = System.IO.Path.GetFullPath("../../../../Images");
            dialog.InitialDirectory = projectRootFolder;
            bool? response = dialog.ShowDialog();
            if (response == true)
            {
                foreach (var path in dialog.FileNames)
                {
                    images_paths.Add(Tuple.Create(System.IO.File.ReadAllBytes(path), path));
                }
            }
            Grid_Construct();
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
                    System.Windows.Controls.Image image1 = new()
                    {
                        Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(images_paths[i - 1].Item1)
                    };
                    System.Windows.Controls.Image image2 = new()
                    {
                        Source = (BitmapSource)new ImageSourceConverter().ConvertFrom(images_paths[i - 1].Item1)
                    };

                    Grid.SetColumn(image1, 0);
                    Grid.SetRow(image1, i);
                    _ = table.Children.Add(image1);

                    Grid.SetColumn(image2, i);
                    Grid.SetRow(image2, 0);
                    _ = table.Children.Add(image2);
                }
            }
        }
        public void Grid_Clear()
        {
            cancelTokenSource = new();
            token = cancelTokenSource.Token;

            int size = images_paths.Count;
            if (size == 0)
            {
                return;
            }
            else
            {
                table.Children.Clear();
                progress_bar.Value = 0;
                for (int i = 0; i < size + 1; i++)
                {
                    table.RowDefinitions.Clear();
                    table.ColumnDefinitions.Clear();
                }
                images_paths.Clear();
            }
        }

        private async Task EmbeddingsMatrixAsync(List<Tuple<byte[], string>> images_paths, CancellationToken token, List<Task<float[]>> tasks)
        {
            int step1 = 50 / images_paths.Count;
            var image_task = new List<int>();
            list_images.Clear();
            for (int i = 0; i < images_paths.Count; i++)
            {
                try
                {
                    Image db_face = Image.CheckHash(images_paths[i]);
                    if(db_face is null)
                    {
                        var face = SixLabors.ImageSharp.Image.Load<Rgb24>(images_paths[i].Item2);
                        list_images.Add(face);
                        var task1 = AF.GetEmbeddings(face, token);
                        tasks.Add(task1);
                        await tasks[i];
                        using var DB = new DataBase();
                        var face_info = new Image_Info { Data = images_paths[i].Item1 };
                        var face_emb = new byte[tasks[i].Result.Length * 4];
                        Buffer.BlockCopy(tasks[i].Result, 0, face_emb, 0, face_emb.Length);
                        Image new_face = new()
                        {
                            Name = images_paths[i].Item2,
                            Embedding = face_emb,
                            Details = face_info,
                            Hash = Image.GetHash(images_paths[i].Item1)
                        };
                        DB.Add(new_face);
                        DB.SaveChanges();
                    }
                    else
                    {
                        list_images.Add(SixLabors.ImageSharp.Image.Load<Rgb24>(db_face.Name));
                        var face_emb = new float[db_face.Embedding.Length / 4];
                        Buffer.BlockCopy(db_face.Embedding, 0, face_emb, 0, db_face.Embedding.Length);
                    }
                    progress_bar.Value += step1;


                }
                catch (OperationCanceledException e1)
                {
                    Console.WriteLine($"{nameof(OperationCanceledException)} thrown with message: {e1.Message}");
                }
            }
            for (int i = 0; i < tasks.Count; i++)
            {
                try
                {
                    
                }
                catch (OperationCanceledException e2)
                {
                    Console.WriteLine($"{nameof(OperationCanceledException)} thrown with message: {e2.Message}");
                }
            }
        }
        private async Task DistSimilMatrixAsync(CancellationToken token)
        {
            int step2 = 50 / (images_paths.Count * images_paths.Count);

            for (int i = 0; i < images_paths.Count; i++)
            {
                for (int j = 0; j < images_paths.Count; j++)
                {
                    Label cell_result = new();
                    Grid.SetColumn(cell_result, i + 1);
                    Grid.SetRow(cell_result, j + 1);
                    cell_result.HorizontalAlignment = HorizontalAlignment.Center;
                    cell_result.VerticalAlignment = VerticalAlignment.Center;
                    cell_result.FontSize = 12;
                    if (token.IsCancellationRequested)
                    {
                        _ = MessageBox.Show("Calculations braked.");
                        return;
                    }
                    token.ThrowIfCancellationRequested();
                    float dist = await AF.Distance(list_images[i], list_images[j], token);
                    float sim = await AF.Similarity(list_images[i], list_images[j], token);
                    cell_result.Content = $"Distance: {dist * dist}\n Similarity: {sim}";
                    progress_bar.Value += step2;

                    _ = table.Children.Add(cell_result);
                }
            }
            if (!token.IsCancellationRequested)
            {
                progress_bar.Value = 100;
            }
        }
        private async void Start_calc_ClickAsync()
        {
            List<Task<float[]>> tasks = new();
            await EmbeddingsMatrixAsync(images_paths, token, tasks);
            await DistSimilMatrixAsync(token);
            Start_calc.IsEnabled = true;
            Clear_calc.IsEnabled = true;
        }
        private void Start_calc_Click(object sender, RoutedEventArgs e)
        {
            if (images_paths.Count == 0)
            {
                _ = MessageBox.Show("Load some images.");
                return;
            }
            Start_calc.IsEnabled = false;
            Clear_calc.IsEnabled = false;
            Start_calc_ClickAsync();
        }
        private void Load_image_Click(object sender, RoutedEventArgs e)
        {
            Grid_Clear();
            Load();
            Grid_Construct();
        }
        private void Clear_calc_Click(object sender, RoutedEventArgs e)
        {
            Grid_Clear();
        }
        private void Stop_calc_Click(object sender, RoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
        }
        private void Open_Database_Click(object sender, RoutedEventArgs e)
        {
            DB_Window DBW = new();
            DBW.ShowDialog();
        }

    }

}


