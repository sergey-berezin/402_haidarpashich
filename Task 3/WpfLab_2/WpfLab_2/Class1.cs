using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WpfLab_2
{

    public class Image
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Hash { get; set; }
        public byte[] Embedding { get; set; }
        public Image_Info Details { get; set; }

        public static string GetHash(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                return string.Concat(sha256.ComputeHash(data).Select(x => x.ToString("X2")));
            }
        }
        public static Image CheckHash(Tuple<byte[], string> image)
        {
            using (var DB = new DataBase())
            {
                string hash = GetHash(image.Item1);
                var sim_hash = DB.Images.Where(x => x.Hash == hash);
                var det = sim_hash.Include(x => x.Details);
                var im = det.Where(x => Equals(x.Details.Data, image.Item1));
                if (im.Any())
                {
                    return im.First();
                }
                return null;
            }
            
        }
    }
    public class Image_Info
    {
        public int Id { get; set; }
        public byte[] Data { get; set; }
    }
    public class DataBase : DbContext
    {
        public DbSet<Image> Images { get; set; }
        public DbSet<Image_Info> Details { get; set; }

        public DataBase()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder o)
        {
            o.UseSqlite("Data Source=images.db");
        }
    }
}
