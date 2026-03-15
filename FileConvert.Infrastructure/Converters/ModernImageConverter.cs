using System;
using System.IO;
using System.Threading.Tasks;
using FileConvert.Core.Interfaces;
using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using CoreJ2K;
using CoreJ2K.ImageSharp;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles modern image format conversions (HEIC, AVIF, JXL, DNG, JP2/J2K).
    /// Uses SkiaSharp for format support and CoreJ2K for JPEG 2000.
    /// </summary>
    public class ModernImageConverter : IModernImageConverter
    {
        private const long MaxUncompressedSize = 1024 * 1024 * 500; // 500MB max

        static ModernImageConverter()
        {
            // CoreJ2K.ImageSharp requires registration for ImageSharp support
            ImageSharpImageCreator.Register();
        }

        #region HEIC/HEIF Conversions

        public Task<MemoryStream> ConvertHeicToJpg(MemoryStream heicStream) =>
            ConvertModernImageFormat(heicStream, SKEncodedImageFormat.Jpeg, 90, "HEIC/HEIF");

        public Task<MemoryStream> ConvertHeicToPng(MemoryStream heicStream) =>
            ConvertModernImageFormat(heicStream, SKEncodedImageFormat.Png, 0, "HEIC/HEIF");

        public Task<MemoryStream> ConvertHeicToWebP(MemoryStream heicStream) =>
            ConvertModernImageFormat(heicStream, SKEncodedImageFormat.Webp, 90, "HEIC/HEIF");

        #endregion

        #region AVIF Conversions

        public Task<MemoryStream> ConvertAvifToJpg(MemoryStream avifStream) =>
            ConvertModernImageFormat(avifStream, SKEncodedImageFormat.Jpeg, 90, "AVIF");

        public Task<MemoryStream> ConvertAvifToPng(MemoryStream avifStream) =>
            ConvertModernImageFormat(avifStream, SKEncodedImageFormat.Png, 0, "AVIF");

        public Task<MemoryStream> ConvertAvifToWebP(MemoryStream avifStream) =>
            ConvertModernImageFormat(avifStream, SKEncodedImageFormat.Webp, 90, "AVIF");

        #endregion

        #region JPEG XL Conversions

        public Task<MemoryStream> ConvertJxlToJpg(MemoryStream jxlStream) =>
            ConvertModernImageFormat(jxlStream, SKEncodedImageFormat.Jpeg, 90, "JPEG XL");

        public Task<MemoryStream> ConvertJxlToPng(MemoryStream jxlStream) =>
            ConvertModernImageFormat(jxlStream, SKEncodedImageFormat.Png, 0, "JPEG XL");

        public Task<MemoryStream> ConvertJxlToWebP(MemoryStream jxlStream) =>
            ConvertModernImageFormat(jxlStream, SKEncodedImageFormat.Webp, 90, "JPEG XL");

        #endregion

        #region DNG Conversions

        public Task<MemoryStream> ConvertDngToJpg(MemoryStream dngStream) =>
            ConvertModernImageFormat(dngStream, SKEncodedImageFormat.Jpeg, 90, "DNG");

        public Task<MemoryStream> ConvertDngToPng(MemoryStream dngStream) =>
            ConvertModernImageFormat(dngStream, SKEncodedImageFormat.Png, 0, "DNG");

        public Task<MemoryStream> ConvertDngToWebP(MemoryStream dngStream) =>
            ConvertModernImageFormat(dngStream, SKEncodedImageFormat.Webp, 90, "DNG");

        #endregion

        #region JPEG 2000 Conversions

        public Task<MemoryStream> ConvertJp2ToPng(MemoryStream jp2Stream)
        {
            var outputStream = new MemoryStream();
            jp2Stream.Position = 0;

            var decodedImage = J2kImage.FromStream(jp2Stream);
            using (var image = decodedImage.As<Image<Rgb24>>())
            {
                image.SaveAsPng(outputStream);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertJp2ToJpg(MemoryStream jp2Stream)
        {
            var outputStream = new MemoryStream();
            jp2Stream.Position = 0;

            var decodedImage = J2kImage.FromStream(jp2Stream);
            using (var image = decodedImage.As<Image<Rgb24>>())
            {
                image.SaveAsJpeg(outputStream);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertJp2ToWebP(MemoryStream jp2Stream)
        {
            var outputStream = new MemoryStream();
            jp2Stream.Position = 0;

            var decodedImage = J2kImage.FromStream(jp2Stream);
            using (var image = decodedImage.As<Image<Rgb24>>())
            {
                image.SaveAsWebp(outputStream);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region Helper Methods

        private Task<MemoryStream> ConvertModernImageFormat(MemoryStream inputStream, SKEncodedImageFormat targetFormat, int quality, string formatName)
        {
            if (inputStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input image exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            inputStream.Position = 0;

            SKBitmap bitmap;
            try
            {
                bitmap = SKBitmap.Decode(inputStream);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to decode {formatName} image: {ex.Message}", ex);
            }

            if (bitmap == null)
                throw new InvalidOperationException($"Failed to decode {formatName} image. The format may not be supported on this platform.");

            using (bitmap)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(targetFormat, quality);

                var outputStream = new MemoryStream();
                data.SaveTo(outputStream);
                outputStream.Position = 0;
                return Task.FromResult(outputStream);
            }
        }

        #endregion
    }
}
