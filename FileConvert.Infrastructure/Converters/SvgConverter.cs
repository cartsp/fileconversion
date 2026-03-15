using System;
using System.IO;
using System.Threading.Tasks;
using FileConvert.Core.Interfaces;
using SkiaSharp;
using Svg.Skia;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles SVG format conversions.
    /// Uses Svg.Skia for vector to raster conversion.
    /// </summary>
    public class SvgConverter : ISvgConverter
    {
        public Task<MemoryStream> ConvertToPng(MemoryStream svgStream)
            => ConvertSvgToRaster(svgStream, SKEncodedImageFormat.Png, SKColors.Transparent, 100);

        public Task<MemoryStream> ConvertToJpg(MemoryStream svgStream)
            => ConvertSvgToRaster(svgStream, SKEncodedImageFormat.Jpeg, SKColors.White, 80);

        public Task<MemoryStream> ConvertToWebP(MemoryStream svgStream)
            => ConvertSvgToRaster(svgStream, SKEncodedImageFormat.Webp, SKColors.Transparent, 80);

        private Task<MemoryStream> ConvertSvgToRaster(
            MemoryStream svgStream,
            SKEncodedImageFormat format,
            SKColor backgroundColor,
            int quality)
        {
            var outputStream = new MemoryStream();
            svgStream.Position = 0;

            using (var svg = new SKSvg())
            {
                svg.Load(svgStream);

                if (svg.Picture != null)
                {
                    var dimensions = svg.Picture.CullRect;
                    var width = (int)Math.Ceiling(dimensions.Width);
                    var height = (int)Math.Ceiling(dimensions.Height);

                    if (width <= 0 || height <= 0)
                    {
                        width = 800;
                        height = 600;
                    }

                    using (var bitmap = new SKBitmap(width, height))
                    using (var canvas = new SKCanvas(bitmap))
                    {
                        canvas.Clear(backgroundColor);
                        canvas.DrawPicture(svg.Picture);
                        canvas.Flush();

                        using (var data = bitmap.Encode(format, quality))
                        {
                            data.SaveTo(outputStream);
                        }
                    }
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }
    }
}
