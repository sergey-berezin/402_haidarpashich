using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ImageAsynx;
using Microsoft.EntityFrameworkCore;

namespace WpfLab_2
{
    public partial class DB_Window : Window
    {
        public ObservableCollection<Image> ImagesData { get; private set; }
        public DB_Window()
        {
            ImagesData = new ObservableCollection<Image>();

            using (var DB = new DataBase())
            {
                foreach (var image in DB.Images)
                {
                    ImagesData.Add(image);
                }
            }

            InitializeComponent();
            DataContext = this;
        }

        private void Delete_Image_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var image = ImagesData[Images_Data.SelectedIndex];
                using var DB = new DataBase();
                var del_image = DB.Images.Where(x => x.Id == image.Id).Include(x => x.Details).First();
                if (del_image is null)
                {
                    return;
                }
                else
                {
                    DB.Details.Remove(del_image.Details);
                    DB.Images.Remove(del_image);
                    DB.SaveChanges();
                    ImagesData.Remove(image);
                }
               
            }
            catch (Exception e1)
            {
                MessageBox.Show(e1.Message);
            }
        }
    }
}
