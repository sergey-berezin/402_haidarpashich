using System;
using System.Threading;
using System.Threading.Tasks;
using ImageAsynx;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Testing_NuGetArcFace
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (ArcFace component = new())
            {
                using var face1 = Image.Load<Rgb24>("face1.png");
                using var face2 = Image.Load<Rgb24>("face2.png");

                CancellationTokenSource cts = new CancellationTokenSource();
                CancellationToken ct = cts.Token;

                var dist = await component.Distance(face1, face2, ct);
                var sim = await component.Similarity(face1, face2, ct);

                Console.WriteLine("Predicting contents of image...");
                Console.WriteLine("Distance =  {0:N3}", dist * dist);
                Console.WriteLine("Similarity = {0:N3}", sim);

                var dist_same = await component.Distance(face1, face1, ct);
                var sim_same = await component.Similarity(face1, face1, ct);

                Console.WriteLine("Predicting contents of image...");
                Console.WriteLine("Distance =  {0:N3}", dist_same * dist_same);
                Console.WriteLine("Similarity = {0:N3}", sim_same);
            }
           
        }
    }
}
