using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileConvert.Core.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles PDF-related conversions.
    /// Uses PdfPig for PDF parsing and QuestPDF/SkiaSharp for generation.
    /// </summary>
    public class PdfConverter : IPdfConverter
    {
        private const long MaxUncompressedSize = 1024 * 1024 * 500; // 500MB max
        private const int MaxTextLinesForImageConversion = 10000;

        static PdfConverter()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public Task<MemoryStream> ConvertImageToPdf(MemoryStream imageStream)
        {
            var outputStream = new MemoryStream();
            imageStream.Position = 0;

            var imageData = imageStream.ToArray();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0, Unit.Point);

                    page.Content()
                        .AlignCenter()
                        .AlignMiddle()
                        .Image(imageData)
                        .FitArea();
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ConvertPdfToPng(MemoryStream pdfStream)
        {
            return Task.FromResult(ConvertPdfToImage(pdfStream, SKEncodedImageFormat.Png, SKColors.White, 100));
        }

        public Task<MemoryStream> ConvertPdfToJpg(MemoryStream pdfStream)
        {
            return Task.FromResult(ConvertPdfToImage(pdfStream, SKEncodedImageFormat.Jpeg, SKColors.White, 80));
        }

        public async Task<MemoryStream> ConvertPdfToText(MemoryStream pdfStream)
        {
            pdfStream.Position = 0;

            using (var document = PdfDocument.Open(pdfStream))
            {
                var textBuilder = new StringBuilder();

                foreach (var page in document.GetPages())
                {
                    var pageText = page.Text;
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine(pageText);
                        textBuilder.AppendLine();
                    }
                }

                var extractedText = textBuilder.ToString().Trim();
                return await WriteStringToStreamAsync(extractedText);
            }
        }

        public Task<MemoryStream> MergePdfs(List<MemoryStream> pdfStreams)
        {
            if (pdfStreams == null || pdfStreams.Count == 0)
            {
                throw new ArgumentException("No PDF streams provided for merging");
            }

            foreach (var stream in pdfStreams)
            {
                stream.Position = 0;
            }

            var tempFiles = new List<string>();
            var outputStream = new MemoryStream();

            try
            {
                foreach (var pdfStream in pdfStreams)
                {
                    var tempFile = Path.GetTempFileName() + ".pdf";
                    File.WriteAllBytes(tempFile, pdfStream.ToArray());
                    tempFiles.Add(tempFile);
                }

                byte[] mergedBytes;
                if (tempFiles.Count == 1)
                {
                    mergedBytes = File.ReadAllBytes(tempFiles[0]);
                }
                else
                {
                    mergedBytes = PdfMerger.Merge(tempFiles.ToArray());
                }

                outputStream = new MemoryStream(mergedBytes);
                outputStream.Position = 0;
            }
            finally
            {
                foreach (var tempFile in tempFiles)
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }

            return Task.FromResult(outputStream);
        }

        public Task<MemoryStream> ExtractPage(MemoryStream pdfStream, int pageNumber)
        {
            pdfStream.Position = 0;

            using (var document = PdfDocument.Open(pdfStream))
            {
                if (pageNumber < 1 || pageNumber > document.NumberOfPages)
                {
                    throw new ArgumentException($"Page number {pageNumber} is out of range. Document has {document.NumberOfPages} pages.");
                }

                var builder = new PdfDocumentBuilder();
                builder.AddPage(document, pageNumber);

                var pageBytes = builder.Build();
                var outputStream = new MemoryStream(pageBytes);
                outputStream.Position = 0;
                return Task.FromResult(outputStream);
            }
        }

        private MemoryStream ConvertPdfToImage(
            MemoryStream pdfStream,
            SKEncodedImageFormat format,
            SKColor backgroundColor,
            int quality)
        {
            if (pdfStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            pdfStream.Position = 0;

            PdfDocument document;
            try
            {
                document = PdfDocument.Open(pdfStream);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse PDF document: {ex.Message}", ex);
            }

            using var pdfDoc = document;

            Page firstPage;
            try
            {
                firstPage = pdfDoc.GetPages().FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read PDF pages: {ex.Message}", ex);
            }

            if (firstPage == null)
            {
                throw new ArgumentException("PDF document contains no pages");
            }

            var pageText = firstPage.Text;

            if (string.IsNullOrWhiteSpace(pageText))
            {
                throw new ArgumentException("PDF page contains no extractable text content");
            }

            const int width = 1240;
            const int height = 1754;
            const int margin = 60;
            const int lineHeight = 24;
            const int maxCharsPerLine = 90;

            var outputStream = new MemoryStream();

            using (var bitmap = new SKBitmap(width, height))
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(backgroundColor);

                using var paint = new SKPaint
                {
                    Color = SKColors.Black,
                    IsAntialias = true
                };
                using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 16);

                var textLines = pageText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var wrappedLines = new List<string>();

                foreach (var line in textLines)
                {
                    if (wrappedLines.Count >= MaxTextLinesForImageConversion)
                        break;

                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    var currentLine = "";
                    foreach (var word in trimmedLine.Split(' '))
                    {
                        if ((currentLine + " " + word).Trim().Length <= maxCharsPerLine)
                        {
                            currentLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(currentLine))
                                wrappedLines.Add(currentLine);
                            currentLine = word;
                        }
                    }
                    if (!string.IsNullOrEmpty(currentLine))
                        wrappedLines.Add(currentLine);
                }

                float y = margin;
                foreach (var textLine in wrappedLines.Take((height - 2 * margin) / lineHeight))
                {
                    canvas.DrawText(textLine, margin, y, font, paint);
                    y += lineHeight;
                }

                canvas.Flush();

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(format, quality);
                data.SaveTo(outputStream);
            }

            outputStream.Position = 0;
            return outputStream;
        }

        private static async Task<MemoryStream> WriteStringToStreamAsync(string content)
        {
            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                await writer.WriteAsync(content);
            }
            outputStream.Position = 0;
            return outputStream;
        }
    }
}
