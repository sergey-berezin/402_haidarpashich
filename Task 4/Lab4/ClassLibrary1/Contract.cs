using System.Security.Cryptography;


namespace Contract
{
    public class Image
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Hash { get; set; }
        public byte[] Embedding { get; set; }
        public Imdet Details { get; set; }

        public static string GetHash(byte[] data)
        {
            return string.Concat(SHA256.HashData(data).Select(x => x.ToString("X2")));
        }
    }
    public class Imdet
    {
        public int Id { get; set; }
        public byte[]? Data { get; set; }
    }
    
    public class Imtruc
    {
        public byte[] Image { get; set; }
        public string Path { get; set; }

        public Imtruc(byte[] image, string path)
        {
            Image = image;
            Path = path;
        }
    }
}
