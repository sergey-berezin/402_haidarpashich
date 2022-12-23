using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using Contract;
using System.Net.Http;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;

namespace Client
{
    public partial class MainWindow : Window
    {
        private readonly string URL = "http://localhost:5187/api/images";
        private readonly string URL_COMP = "http://localhost:5187/api/compare";
        private const int MAX_CALLS = 3;
        private CancellationTokenSource cancelTokenSource;
        private CancellationToken token;
        private bool calculations_status;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly List<Imtruc> list_ds;

        public MainWindow()
        {
            InitializeComponent();
            cancelTokenSource = new();
            token = cancelTokenSource.Token;
            calculations_status = false;
            list_ds = new();
            _retryPolicy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(MAX_CALLS, times =>
                 TimeSpan.FromMilliseconds(Math.Exp(times) * 250));
        }


        private void Load_image_Click(object sender, RoutedEventArgs e)
        {
            Grid_Clear();
            Microsoft.Win32.OpenFileDialog ofd = new();
            ofd.Multiselect = true;
            ofd.Filter = "Images (*.jpg, *.png)|*.jpg;*.png";
            var projectRootFolder = System.IO.Path.GetFullPath("../../../Images");
            ofd.InitialDirectory = projectRootFolder;
            var response = ofd.ShowDialog();
            if (response == true)
            {
                foreach (var path in ofd.FileNames)
                {           
                    Imtruc obj = new(System.IO.File.ReadAllBytes(path), path);
                    list_ds.Add(obj);
                }
            }
            Grid_Construct();
        }

        private void Grid_Construct()
        {
            int n = list_ds.Count;
            for (int i = 0; i < n + 1; i++)
            {
                table.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });
                table.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

                if (i > 0)
                {
                    var image1 = new System.Windows.Controls.Image
                    {
                        Source = new ImageSourceConverter().ConvertFrom(list_ds[i - 1].Image) as BitmapSource
                    };

                    var image2 = new System.Windows.Controls.Image
                    {
                        Source = new ImageSourceConverter().ConvertFrom(list_ds[i - 1].Image) as BitmapSource
                    };

                    Grid.SetColumn(image1, 0);
                    Grid.SetRow(image1, i);
                    table.Children.Add(image1);

                    Grid.SetColumn(image2, i);
                    Grid.SetRow(image2, 0);
                    table.Children.Add(image2);
                }
            }
        }

        public void Grid_Clear()
        {
            calculations_status = false;
            cancelTokenSource = new();
            token = cancelTokenSource.Token;
            int size = list_ds.Count;
            if (size == 0)
                return;
            table.Children.Clear();
            pbStatus.Value = 0;
            for (int i = 0; i < size + 1; i++)
            {
                table.RowDefinitions.Clear();
            }
            for (int i = 0; i < size + 1; i++)
            {
                table.ColumnDefinitions.Clear();
            }
            list_ds.Clear();
        }
        public async Task<List<int>> CalculateImage(List<Imtruc> list_images)
        {
            try
            {
                var serial = JsonConvert.SerializeObject(list_images);
                var content = new StringContent(serial);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    HttpClient client = new HttpClient();
                    var task = await client.PostAsync(URL, content, token);
                    var task_result = JsonConvert.DeserializeObject<List<int>>(task.Content.ReadAsStringAsync().Result);
                    return task_result;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                throw new Exception($"Request was failed {MAX_CALLS} times with URL: \"{URL}\"");
            }
        }
        async Task<float> Distance(float[] v1, float[] v2)
        {
            return await Task<float>.Factory.StartNew(() => {
                return Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());
            });
        }

        async Task<float> Similarity(float[] v1, float[] v2)
        {
            return await Task<float>.Factory.StartNew(() =>
            {
                return v1.Zip(v2).Select(p => p.First * p.Second).Sum();
            });
        }
        float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x * x).Sum());

        private async void Start_calc_Click(object sender, RoutedEventArgs e)
        {
            if (list_ds.Count == 0)
            {
                MessageBox.Show("Load images.");
                return;
            }
            if (calculations_status)
            {
                MessageBox.Show("Already calculated. Refresh catalog.");
                return;
            }
            int step1 = 50 / list_ds.Count;
            int step2 = 50 / (list_ds.Count * list_ds.Count);
            try
            {
                var task = CalculateImage(list_ds);
                await task;
                bool check = true;
                for (int i = 0; i < list_ds.Count; i++)
                {
                    for (int j = 0; j < list_ds.Count; j++)
                    {
                        Label label = new();
                        Grid.SetColumn(label, i + 1);
                        Grid.SetRow(label, j + 1);
                        label.HorizontalAlignment = HorizontalAlignment.Center;
                        label.VerticalAlignment = VerticalAlignment.Center;
                        label.FontSize = 12;
                        try
                        {
                            List<int> list_id = new();
                            List<float> dist_sim = new();
                            if(task.Result.Count <= i || task.Result.Count <= j)
                            {
                                MessageBox.Show("Calculations aborted.");
                                check = false;
                                break;
                            }
                            list_id.Add(task.Result[i]);
                            list_id.Add(task.Result[j]);
                            var serial = JsonConvert.SerializeObject(list_id);
                            var content = new StringContent(serial);
                            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            await _retryPolicy.ExecuteAsync(async () =>
                            {
                                HttpClient client = new();
                                var task = await client.PostAsync(URL_COMP, content, token);
                                var task1_result = JsonConvert.DeserializeObject<List<float>>(task.Content.ReadAsStringAsync().Result);
                                dist_sim = task1_result;
                            });
                            if (dist_sim.Count != 2)
                            {
                                label.Content = $"Distance: Not calculated\n Similarity: Not calculated";
                            }
                            else
                            {
                                label.Content = $"Distance: {dist_sim[0]}\n Similarity: {dist_sim[1]}";
                                pbStatus.Value += step2;
                            }
                            table.Children.Add(label);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            throw new Exception($"Request was failed {MAX_CALLS} times with URL: \"{URL}\"");
                        }
                    }
                }
                if (!token.IsCancellationRequested || check)
                {
                    pbStatus.Value = 100;
                }
                calculations_status = true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Clear_calc_Click(object sender, RoutedEventArgs e)
        {
            Grid_Clear();
        }

        private void Open_Database_Click(object sender, RoutedEventArgs e)
        {
            DataBaseWindow dbw = new();
            dbw.ShowDialog();
        }

        private void Stop_calc_Click(object sender, RoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
            MessageBox.Show("Calculations aborted.");
        }
    }
}