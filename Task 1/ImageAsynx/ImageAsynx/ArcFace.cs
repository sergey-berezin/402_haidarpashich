using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace ImageAsynx
{
    public class ArcFace : IDisposable
    {
        private InferenceSession session;

        public ArcFace()
        {
            using var modelStream = typeof(ArcFace).Assembly.GetManifestResourceStream("ImageAsynx.arcfaceresnet100-8.onnx");
            using var memoryStream = new MemoryStream();
            modelStream.CopyTo(memoryStream);
            session = new InferenceSession(memoryStream.ToArray());
        }

        float Length(float[] v) => (float)Math.Sqrt(v.Select(x => x * x).Sum());

        public async Task<float> Distance(Image<Rgb24> face1, Image<Rgb24> face2, CancellationToken ct)
        {
            var v1 = await GetEmbeddings(face1, ct);
            var v2 = await GetEmbeddings(face2, ct);
            return await Task<float>.Factory.StartNew(() =>
            {
                
                ct.ThrowIfCancellationRequested();
                return Length(v1.Zip(v2).Select(p => p.First - p.Second).ToArray());
            }, ct);
            
        }
        public async Task<float> Similarity(Image<Rgb24> face1, Image<Rgb24> face2, CancellationToken ct)
        {
            var v1 = await GetEmbeddings(face1, ct);
            var v2 = await GetEmbeddings(face2, ct);
            return await Task<float>.Factory.StartNew(() =>
            {
                ct.ThrowIfCancellationRequested();
                return v1.Zip(v2).Select(p => p.First * p.Second).Sum();
            }, ct);

        }

        float[] Normalize(float[] v)
        {
            var len = Length(v);
            return v.Select(x => x / len).ToArray();
        }

        private DenseTensor<float> ImageToTensor(Image<Rgb24> img)
        {
            var w = img.Width;
            var h = img.Height;
            var t = new DenseTensor<float>(new[] { 1, 3, h, w });

            img.ProcessPixelRows(pa =>
            {
                for (int y = 0; y < h; y++)
                {
                    Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                    for (int x = 0; x < w; x++)
                    {
                        t[0, 0, y, x] = pixelSpan[x].R;
                        t[0, 1, y, x] = pixelSpan[x].G;
                        t[0, 2, y, x] = pixelSpan[x].B;
                    }
                }
            });

            return t;
        }

        public async Task<float[]> GetEmbeddings(Image<Rgb24> face, CancellationToken ct)
        {
            return await Task<float[]>.Factory.StartNew(() =>
            {
                {
                    ct.ThrowIfCancellationRequested();
                        
                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("data", ImageToTensor(face)) };
                    IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;
                    lock (session)
                    {
                        results = session.Run(inputs);
                    }
                    
                    return Normalize(results.First(v => v.Name == "fc1").AsEnumerable<float>().ToArray());
                }
            }, ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        }        

        public void Dispose()
        {
            session.Dispose();
        }
        
    }
}
