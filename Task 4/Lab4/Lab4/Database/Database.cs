using ImageAsynx;
using Contract;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;


namespace Server.Database
{
    
    public class IContext : DbContext
    {
        public DbSet<Image> Images { get; set; }
        public DbSet<Imdet> Details { get; set; }
        public IContext()
        {
            Database.EnsureCreated();
        }
        public static Image CheckHash(Tuple<byte[], string> image)
        {
            using var db = new IContext();
            string hash = Image.GetHash(image.Item1);
            var sim_hash = db.Images.Where(x => x.Hash == hash);
            var det = sim_hash.Include(x => x.Details);
            var im = det.Where(x => Equals(x.Details.Data, image.Item1));
            if (im.Any())
            {
                return im.First();
            }
            return null;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder o)
        {
            o.UseSqlite("Data Source=images.db");
        }
    }
    public interface ImDB
    {
        Task<List<Image>> GetImages();
        Task<List<int>> PostImages(List<Imtruc> images_list, CancellationToken token);
        Task<int> DeleteImages(int id);
        Task<List<float>> PostCompare(List<int> id, CancellationToken token);
        Task<float> Distance(float[] v1, float[] v2);
        Task<float> Similarity(float[] v1, float[] v2);
        float Length(float[] v);

    }
    public class ImagesDatabase : ImDB
    {
        readonly ArcFace AF_obj = new();
        public async Task<List<Image>> GetImages()
        {
            var result = new List<Image>();
            try
            {
                using (var db = new IContext())
                {
                    List<Image> photos = db.Images.Include(item => item.Details).ToList();
                    return photos;
                }
            }
            catch (Exception ex)
            {
                return result;
            }
        }
        public async Task<int> DeleteImages(int id)
        {
            try
            {
                using (var db = new IContext())
                {
                    var deletedImage = db.Images.Where(x => x.Id == id).Include(x => x.Details).First();
                    if (deletedImage == null)
                    {
                        return -1;
                    }
                    db.Details.Remove(deletedImage.Details);
                    db.Images.Remove(deletedImage);
                    db.SaveChanges();
                }
                return 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }
        public async Task<List<int>> PostImages(List<Imtruc> images_list, CancellationToken token)
        {
            List<int> result = new();
            try
            {
                
                for(int i = 0; i < images_list.Count; i++)
                {
                    Image existImage = null;
                    Imtruc curr = images_list[i];
                    using (var db = new IContext())   
                    {
                        string hash = Image.GetHash(curr.Image);
                        var q = db.Images.Where(x => x.Hash == hash)
                            .Include(x => x.Details)
                            .Where(x => Equals(x.Details.Data, curr.Image));
                        if (q.Any())
                        {
                            existImage = q.First();
                        }
                    }
                    if (existImage is not null)
                    { 
                        result.Add(existImage.Id);
                    }
                    else                       
                    {
                        var face = SixLabors.ImageSharp.Image.Load<Rgb24>(curr.Path);
                        var task = AF_obj.GetEmbeddings(face, token);
                        await task;
                        using (var db = new IContext())
                        {
                            var newImageDetails = new Imdet { Data = curr.Image };
                            var byteArray = new byte[task.Result.Length * 4];
                            Buffer.BlockCopy(task.Result, 0, byteArray, 0, byteArray.Length);
                            Image newImage = new()
                            {
                                Name = curr.Path,
                                Embedding = byteArray,
                                Details = newImageDetails,
                                Hash = Image.GetHash(curr.Image)
                            };
                            db.Add(newImage);
                            db.SaveChanges();
                        }

                        existImage = null;
                        using (var db = new IContext())
                        {
                            string hash = Image.GetHash(curr.Image);
                            var q = db.Images.Where(x => x.Hash == hash)
                                .Include(x => x.Details)
                                .Where(x => Equals(x.Details.Data, curr));
                            if (q.Any())
                            {
                                existImage = q.First();
                            }
                        }
                        if (existImage is not null)
                        {
                            result.Add(existImage.Id);
                        }
                    }
                }
                return result;
            }
            catch (OperationCanceledException e1)
            {
                return result;
            }
        }

        public async Task<float> Distance(float[] v1, float[] v2)
        {
            return await Task<float>.Factory.StartNew(() => {
                return Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());
            });
        }

        public async Task<float> Similarity(float[] v1, float[] v2)
        {
            return await Task<float>.Factory.StartNew(() =>
            {
                return v1.Zip(v2).Select(p => p.First * p.Second).Sum();
            });
        }
        public float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x * x).Sum());

        public async Task<List<float>> PostCompare(List<int> list_id, CancellationToken token)
        {
            List<float> result = new();
            int id1 = list_id[0];
            int id2 = list_id[1];
            try
            {
                using (var db = new IContext())
                {
                    var curr_im1 = db.Images.Where(x => x.Id == id1).Include(x => x.Details).First();
                    var curr_im2 = db.Images.Where(x => x.Id == id2).Include(x => x.Details).First();
                    float[] embeddings1 = new float[curr_im1.Embedding.Length / 4];
                    for (int t = 0; t < curr_im1.Embedding.Length / 4; t++)
                        embeddings1[t] = BitConverter.ToSingle(curr_im1.Embedding, t * 4);
                    float[] embeddings2 = new float[curr_im2.Embedding.Length / 4];
                    for (int t = 0; t < curr_im2.Embedding.Length / 4; t++)
                        embeddings2[t] = BitConverter.ToSingle(curr_im2.Embedding, t * 4);

                    var task1 = Distance(embeddings1, embeddings2);
                    var task2 = Similarity(embeddings1, embeddings2);
                    await task1;
                    await task2;
                    result.Add(task1.Result);
                    result.Add(task2.Result);
                }
                return result;
            }
            catch (Exception ex)
            {
                return result;
            }
        }
    }
}