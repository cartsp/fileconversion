using System.IO;
using System.Threading.Tasks;
using FileConvert.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles TIFF format conversions.
    /// Uses ImageSharp for cross-platform, WASM-compatible image processing.
    /// </summary>
    public class TiffConverter : ITiffConverter
    {
        private static readonly JpegEncoder CachedJpegEncoder80 = new JpegEncoder { Quality = 80 };

        public Task<MemoryStream> ConvertToPng(MemoryStream tiffStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(tiffStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertToJpg(MemoryStream tiffStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(tiffStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, CachedJpegEncoder80);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertToWebP(MemoryStream tiffStream)
        {
            tiffStream.Position = 0;
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(tiffStream.ToArray()))
            {
                image.SaveAsWebp(outputStream);
            }

            return Task.FromResult(outputStream);
        }
    }
}
