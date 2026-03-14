using FileConvert.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Infrastruture.Tester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "generate-test-files")
            {
                GenerateTestFiles();
                return;
            }

            var conversionService = new FileConversionService();

            MemoryStream pngStream = new MemoryStream();
            var wordDocToConvert = new FileInfo("pnggradHDrgba.png");

            using (FileStream file = new FileStream(wordDocToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.ReadExactly(bytes, 0, (int)file.Length);
                pngStream.Write(bytes, 0, (int)file.Length);
                pngStream.Position = 0;
                var result = await conversionService.ConvertImageTojpg(pngStream);

                FileStream jpgfile = new FileStream("file.jpg", FileMode.Create, FileAccess.Write);
                result.WriteTo(jpgfile);
                jpgfile.Close();
                result.Close();
            }
        }

        static void GenerateTestFiles()
        {
            var outputDir = Path.Combine("FileConvert.UnitTests", "Documents");

            // Create a simple 10x10 red test image
            using var image = new Image<Rgb24>(10, 10);
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    image[x, y] = new Rgb24(255, 0, 0);
                }
            }

            // Save as WebP
            var webpPath = Path.Combine(outputDir, "test.webp");
            image.SaveAsWebp(webpPath);
            Console.WriteLine($"Created {webpPath}");

            // Save as TIFF
            var tiffPath = Path.Combine(outputDir, "test.tif");
            image.SaveAsTiff(tiffPath);
            Console.WriteLine($"Created {tiffPath}");
        }
    }
}
public static class Extensions
{
    public static MemoryStream ConvertToBase64(this MemoryStream stream)
    {
        byte[] bytes;
        using (var memoryStream = new MemoryStream())
        {
            stream.CopyTo(memoryStream);
            bytes = memoryStream.ToArray();
        }

        string base64 = Convert.ToBase64String(bytes);
        return new MemoryStream(Encoding.UTF8.GetBytes(base64));
    }
}
