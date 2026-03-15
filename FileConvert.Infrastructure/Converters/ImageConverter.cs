using System.IO;
using System.Threading.Tasks;
using FileConvert.Core.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles basic image format conversions (JPG, PNG, GIF, WebP, ICO).
    /// Uses ImageSharp for cross-platform, WASM-compatible image processing.
    /// </summary>
    public class ImageConverter : IImageConverter
    {
        private static readonly JpegEncoder CachedJpegEncoder80 = new JpegEncoder { Quality = 80 };

        public Task<MemoryStream> ConvertToJpg(MemoryStream imageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(imageStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, CachedJpegEncoder80);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertToPng(MemoryStream imageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(imageStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertToGif(MemoryStream imageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(imageStream.ToArray()))
            {
                image.SaveAsGif(outputStream);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertToWebP(MemoryStream imageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(imageStream.ToArray()))
            {
                image.SaveAsWebp(outputStream);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertWebPToJpg(MemoryStream webPStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(webPStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, CachedJpegEncoder80);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertWebPToPng(MemoryStream webPStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(webPStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertWebPToGif(MemoryStream webPStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(webPStream.ToArray()))
            {
                image.SaveAsGif(outputStream);
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertImageToIco(MemoryStream imageStream)
        {
            var outputStream = new MemoryStream();
            imageStream.Position = 0;

            using (var image = Image.Load(imageStream))
            {
                IcoFormat.EncodeAsIco(image, outputStream);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertIcoToPng(MemoryStream icoStream)
        {
            var outputStream = new MemoryStream();
            icoStream.Position = 0;

            using (var image = IcoFormat.DecodeFromIco(icoStream))
            {
                image.SaveAsPng(outputStream);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }
    }
}
