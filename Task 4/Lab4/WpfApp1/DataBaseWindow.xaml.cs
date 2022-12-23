using System;
using System.Collections.Generic;
using System.Windows;
using System.Collections.ObjectModel;
using System.Net.Http;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;

namespace Client
{
    public partial class DataBaseWindow : Window
    {
        private readonly string URL = "http://localhost:5187/api/images";
        private const int MAX_CALLS = 3;
        public ObservableCollection<Contract.Image> ImagesData { get; private set; }
        private readonly AsyncRetryPolicy _retryPolicy;

        public DataBaseWindow()
        {
            _retryPolicy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(MAX_CALLS, times =>
                 TimeSpan.FromMilliseconds(Math.Exp(times) * 250));
            ImagesData = new();
            GetImages();
            InitializeComponent();
            DataContext = this;
        }

        public async void GetImages()
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    HttpClient client = new();
                    var task = await client.GetAsync(URL);
                    if (task.IsSuccessStatusCode)
                    {
                        var list_images = JsonConvert.DeserializeObject<List<Contract.Image>>(task.Content.ReadAsStringAsync().Result);
                        for(int i = 0; i < list_images.Count; i++)
                        {
                            ImagesData.Add(list_images[i]);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private async void Delete_Image_Click(object sender, RoutedEventArgs e)
        {
            var image = ImagesData[Images_Data.SelectedIndex];
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    HttpClient client = new();
                    var task = await client.DeleteAsync($"{URL}/{image.Id}");
                    if (task.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<int>(task.Content.ReadAsStringAsync().Result);
                        if (result == 1)
                        {
                            MessageBox.Show("Image was deleted.");
                            ImagesData.Remove(image);
                        }
                        else
                        {
                            MessageBox.Show("Error occurred! Try again.");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}