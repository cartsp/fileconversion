using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FileConvert.Core.Interfaces;
using QRCoder;
using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp;
using ZXing.SkiaSharp.Rendering;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles special conversions (QR codes, barcodes).
    /// Uses QRCoder for QR codes and ZXing for barcodes.
    /// </summary>
    public class SpecialConverter : ISpecialConverter
    {
        public Task<MemoryStream> ConvertTextToQrCodePng(MemoryStream textStream)
        {
            textStream.Position = 0;
            var textContent = Encoding.UTF8.GetString(textStream.ToArray()).Trim();

            if (string.IsNullOrEmpty(textContent))
                throw new ArgumentException("Input text is empty");

            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(textContent, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrCodeData))
                {
                    var qrCodeBytes = qrCode.GetGraphic(20);
                    var outputStream = new MemoryStream(qrCodeBytes);
                    outputStream.Position = 0;
                    return Task.FromResult(outputStream);
                }
            }
        }

        public Task<MemoryStream> ConvertTextToBarcodeJpg(MemoryStream textStream)
        {
            textStream.Position = 0;
            var textContent = Encoding.UTF8.GetString(textStream.ToArray()).Trim();

            if (string.IsNullOrEmpty(textContent))
                throw new ArgumentException("Input text is empty");

            var writer = new BarcodeWriter<SKBitmap>
            {
                Format = BarcodeFormat.CODE_128,
                Options = new ZXing.Common.EncodingOptions
                {
                    Width = 400,
                    Height = 150,
                    Margin = 10
                },
                Renderer = new SKBitmapRenderer()
            };

            using (var bitmap = writer.Write(textContent))
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 90))
            {
                var outputStream = new MemoryStream();
                data.SaveTo(outputStream);
                outputStream.Position = 0;
                return Task.FromResult(outputStream);
            }
        }
    }
}
