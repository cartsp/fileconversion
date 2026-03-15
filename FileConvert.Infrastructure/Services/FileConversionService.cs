using FileConvert.Core;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FileConvert.Core.Entities;
using OfficeOpenXml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using FileConvert.Core.ValueObjects;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Markdig;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using ImageSharpImage = SixLabors.ImageSharp.Image;
using SkiaSharp;
using Svg.Skia;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;
using QRCoder;
using ZXing;
using ZXing.SkiaSharp;
using ZXing.SkiaSharp.Rendering;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using CoreJ2K;
using CoreJ2K.ImageSharp;
using VersOne.Epub;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace FileConvert.Infrastructure
{
    public class FileConversionService : IFileConvertors
    {
        private static readonly MarkdownPipeline CachedMarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        private static readonly JsonSerializerOptions CachedJsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static readonly JpegEncoder CachedJpegEncoder80 = new JpegEncoder { Quality = 80 };
        private static readonly IDeserializer CachedYamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        private static readonly ISerializer CachedYamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        private static readonly Regex MultipleBlankLinesRegex = new(@"\r\n\s*\r\n", RegexOptions.Compiled);
        private static readonly Regex HorizontalWhitespaceRegex = new(@"[ \t]+", RegexOptions.Compiled);
        private const int StreamBufferSize = 4096;
        private const int DefaultZipCompressionLevel = 6; // Balanced compression/speed
        private const long MaxUncompressedSize = 1024 * 1024 * 500; // 500MB max per entry
        private const long MaxTotalUncompressedSize = 1024 * 1024 * 1024; // 1GB max total
        private const int MaxEntryCount = 10000;
        private const int MaxTextLinesForImageConversion = 10000; // Maximum lines to process for image conversion
        private const int MaxTextContentLength = 1000000; // Maximum text content length in characters
        private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
        {
            "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6", "li", "tr"
        };
        private static IImmutableList<ConvertorDetails> Convertors = ImmutableList<ConvertorDetails>.Empty;

        /// <summary>
        /// Sanitizes an archive entry path to prevent path traversal attacks.
        /// Uses a secure approach that validates the final path doesn't escape the root.
        /// </summary>
        /// <param name="entryPath">The original entry path from the archive</param>
        /// <returns>A sanitized path safe for use in the output archive</returns>
        private static string SanitizeArchiveEntryPath(string entryPath)
        {
            if (string.IsNullOrWhiteSpace(entryPath))
                return "unknown";

            // Normalize path separators and remove leading slashes
            var normalizedPath = entryPath.Replace('\\', '/').TrimStart('/');

            // Split into path components and filter out dangerous ones
            var components = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var safeComponents = new List<string>();

            foreach (var component in components)
            {
                // Skip empty, current directory, and parent directory references
                if (string.IsNullOrEmpty(component) || component == "." || component == "..")
                    continue;

                // Skip components that could be dangerous on Windows
                if (component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    // Replace invalid characters with underscore
                    var safeComponent = new string(component.Select(c =>
                        Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
                    if (!string.IsNullOrEmpty(safeComponent))
                        safeComponents.Add(safeComponent);
                }
                else
                {
                    safeComponents.Add(component);
                }
            }

            // Reconstruct the path
            var safePath = string.Join("/", safeComponents);

            // Final validation: ensure the path doesn't start with .. or contain path traversal patterns
            if (string.IsNullOrEmpty(safePath) || safePath.StartsWith("..") || safePath.Contains("/../"))
                return "unknown";

            return safePath;
        }

        static FileConversionService()
        {
            // EPPlus 5+ requires license context to be set
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            // QuestPDF requires license - Community Edition is free for non-commercial use
            QuestPDF.Settings.License = LicenseType.Community;
            // CoreJ2K.ImageSharp requires registration for ImageSharp support
            ImageSharpImageCreator.Register();
        }

        public FileConversionService()
        {
            CreateConvertorList();
        }

        public void CreateConvertorList()
        {
            var ConvertorListBuilder = ImmutableList.CreateBuilder<ConvertorDetails>(); // returns ImmutableList.Builder

            //ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.xls, ConvertCSVToExcel));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.xlsx, ConvertCSVToExcel));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xlsx, FileExtension.csv, ConvertXLSXToCSV));

            // DOCX conversions - high value document conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.docx, FileExtension.pdf, ConvertDocxToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.docx, FileExtension.html, ConvertDocxToHtml));

            // XLSX to PDF conversion - high value spreadsheet conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xlsx, FileExtension.pdf, ConvertXlsxToPdf));

            // PPTX to PDF conversion - high value presentation conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.pdf, ConvertPptxToPdf));

            // PDF to Image conversions - extract content and render to images
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.png, ConvertPdfToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.jpg, ConvertPdfToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.jpeg, ConvertPdfToJpg));

            // PPTX to Image conversions - render slides to images
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.png, ConvertPptxToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.jpg, ConvertPptxToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.jpeg, ConvertPptxToJpg));

            // HTML to PDF conversion - high value document conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.html, FileExtension.pdf, ConvertHtmlToPdf));

            //ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.mp3, FileExtension.wav, ConvertMP3ToWav));
            //ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.png, ConverTifToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.jpg, ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.jpeg, ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.jpeg, ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.jpg, ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.jpeg, ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.jpg, ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.gif, ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.gif, ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.gif, ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jfif, FileExtension.gif, ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.gif, ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.png, ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.png, ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.png, ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jfif, FileExtension.png, ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.png, ConvertImageToPNG));
            //ConvertorListBuilder.Add(new ConvertorDetails(".png", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".gif", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".jpg", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".jpeg", ".bmp", ConvertImageToBMP));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.xml, ConvertJSONToXML));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.json, ConvertXMLToJSON));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.md, FileExtension.html, ConvertMarkdownToHTML));
            // YAML ↔ JSON conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.yaml, FileExtension.json, ConvertYAMLToJSON));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.yml, FileExtension.json, ConvertYAMLToJSON));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.yaml, ConvertJSONToYAML));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.yml, ConvertJSONToYAML));
            // XLSX → JSON conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xlsx, FileExtension.json, ConvertXLSXToJSON));
            // TSV → CSV conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tsv, FileExtension.csv, ConvertTSVToCSV));
            // CSV ↔ JSON conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.json, ConvertCSVToJSON));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.csv, ConvertJSONToCSV));
            // HTML → Text conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.html, FileExtension.txt, ConvertHTMLToText));

            // WebP conversions - to WebP
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.webp, ConvertImageToWebP));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.webp, ConvertImageToWebP));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.webp, ConvertImageToWebP));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.webp, ConvertImageToWebP));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.webp, ConvertImageToWebP));

            // WebP conversions - from WebP
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.jpg, ConvertWebPToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.jpeg, ConvertWebPToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.png, ConvertWebPToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.gif, ConvertWebPToGif));

            // TIFF conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.png, ConvertTiffToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.jpg, ConvertTiffToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.jpeg, ConvertTiffToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.webp, ConvertTiffToWebP));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.png, ConvertTiffToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.jpg, ConvertTiffToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.jpeg, ConvertTiffToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.webp, ConvertTiffToWebP));

            // TSV → JSON conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tsv, FileExtension.json, ConvertTSVToJSON));

            // XML → CSV conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.csv, ConvertXMLToCSV));

            // CSV → YAML conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.yaml, ConvertCSVToYAML));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.yml, ConvertCSVToYAML));

            // XML ↔ YAML conversions - high value configuration format conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.yaml, ConvertXMLToYAML));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.yml, ConvertXMLToYAML));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.yaml, FileExtension.xml, ConvertYAMLToXML));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.yml, FileExtension.xml, ConvertYAMLToXML));

            // ICO conversions - create favicons from images
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.ico, ConvertImageToIco));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.ico, ConvertImageToIco));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.ico, ConvertImageToIco));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.ico, ConvertImageToIco));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.ico, ConvertImageToIco));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.ico, ConvertImageToIco));

            // ICO → PNG conversion - extract icons
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.ico, FileExtension.png, ConvertIcoToPng));

            // SVG conversions - vector to raster
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.png, ConvertSvgToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.jpg, ConvertSvgToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.jpeg, ConvertSvgToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.webp, ConvertSvgToWebP));

            // Archive format conversions - high value binary conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gz, FileExtension.tar, ConvertGzToTar));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tgz, FileExtension.tar, ConvertGzToTar));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tar, FileExtension.gz, ConvertTarToGz));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tar, FileExtension.tgz, ConvertTarToGz));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bz2, FileExtension.tar, ConvertBz2ToTar));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tbz2, FileExtension.tar, ConvertBz2ToTar));
            // ZIP ↔ TAR conversions - ZIP is the most common archive format
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.zip, FileExtension.tar, ConvertZipToTar));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tar, FileExtension.zip, ConvertTarToZip));

            // 7z and RAR archive conversions - high value binary conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension._7z, FileExtension.zip, Convert7zToZip));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension._7z, FileExtension.tar, Convert7zToTar));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.rar, FileExtension.zip, ConvertRarToZip));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.rar, FileExtension.tar, ConvertRarToTar));

            // JPEG 2000 (JP2/J2K) conversions - high value image format conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.png, ConvertJp2ToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.jpg, ConvertJp2ToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.jpeg, ConvertJp2ToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.webp, ConvertJp2ToWebP));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.png, ConvertJp2ToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.jpg, ConvertJp2ToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.jpeg, ConvertJp2ToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.webp, ConvertJp2ToWebP));

            // Image to PDF conversions - very high value for users
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.pdf, ConvertImageToPdf));

            // Text/URL to QR Code conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.txt, FileExtension.png, ConvertTextToQrCodePng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.qr, FileExtension.png, ConvertTextToQrCodePng));

            // Text/URL to Barcode conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.txt, FileExtension.jpg, ConvertTextToBarcodeJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.txt, FileExtension.jpeg, ConvertTextToBarcodeJpg));

            // PDF to Text conversion - extract text content from PDFs
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.txt, ConvertPdfToText));

            // Markdown to PDF conversion - high value document conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.md, FileExtension.pdf, ConvertMarkdownToPdf));

            // EPUB conversions - high value ebook format conversions
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.epub, FileExtension.pdf, ConvertEpubToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.epub, FileExtension.txt, ConvertEpubToTxt));

            // HEIC/HEIF conversions - iPhone photo format (HIGH VALUE)
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.jpg, ConvertHeicToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.jpeg, ConvertHeicToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.png, ConvertHeicToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.webp, ConvertHeicToWebp));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.jpg, ConvertHeicToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.jpeg, ConvertHeicToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.png, ConvertHeicToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.webp, ConvertHeicToWebp));

            // AVIF conversions - modern image format
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.jpg, ConvertAvifToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.jpeg, ConvertAvifToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.png, ConvertAvifToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.webp, ConvertAvifToWebp));

            // JPEG XL (JXL) conversions - next-gen image format
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.jpg, ConvertJxlToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.jpeg, ConvertJxlToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.png, ConvertJxlToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.webp, ConvertJxlToWebp));

            // DNG conversions - Adobe Digital Negative raw format
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.jpg, ConvertDngToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.jpeg, ConvertDngToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.png, ConvertDngToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.webp, ConvertDngToWebp));

            Convertors = ConvertorListBuilder.ToImmutable();
        }

        public async Task<MemoryStream> ConvertDocToHTML(MemoryStream officeDocStream)
        {
            return await Task.FromResult(officeDocStream);
        }

        //WASM: System.PlatformNotSupportedException: Operation is not supported on this platform.
        //public async Task<MemoryStream> ConvertDocToPDF(MemoryStream officeDocStream)
        //{
        //    var pdfStream = new MemoryStream();

        //    Xceed.Words.NET.Licenser.LicenseKey = "WDN16-Y1WWB-KK8FY-DX8A";
        //    using (pdfStream)
        //    {
        //        using (var wordDoc = Xceed.Words.NET.DocX.Load(officeDocStream))
        //        {
        //            Xceed.Words.NET.DocX.ConvertToPdf(wordDoc, pdfStream);
        //        }
        //        return await Task.FromResult(pdfStream);
        //    }
        //}

        public async Task<MemoryStream> ConvertImageTojpg(MemoryStream PNGStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(PNGStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, CachedJpegEncoder80);
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertImageToPNG(MemoryStream ImageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(ImageStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return await Task.FromResult(outputStream);
        }

        //public async Task<MemoryStream> ConvertImageToBMP(MemoryStream PNGStream)
        //{
        //    MemoryStream outputStream = new MemoryStream();

        //    using (Image image = Image.Load(PNGStream.ToArray()))
        //    {
        //        image.SaveAsBmp(outputStream);
        //    }

        //    return await Task.FromResult(outputStream);
        //}

        public async Task<MemoryStream> ConvertImageToGIF(MemoryStream ImageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(ImageStream.ToArray()))
            {
                image.SaveAsGif(outputStream);
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertImageToWebP(MemoryStream ImageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(ImageStream.ToArray()))
            {
                image.SaveAsWebp(outputStream);
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertWebPToJpg(MemoryStream WebPStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(WebPStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, CachedJpegEncoder80);
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertWebPToPng(MemoryStream WebPStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(WebPStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertWebPToGif(MemoryStream WebPStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(WebPStream.ToArray()))
            {
                image.SaveAsGif(outputStream);
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertTiffToPng(MemoryStream TiffStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(TiffStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertTiffToJpg(MemoryStream TiffStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(TiffStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, CachedJpegEncoder80);
            }

            return await Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a TIFF image to WebP format.
        /// </summary>
        public async Task<MemoryStream> ConvertTiffToWebP(MemoryStream tiffStream)
        {
            tiffStream.Position = 0;
            MemoryStream outputStream = new MemoryStream();

            using (ImageSharpImage image = ImageSharpImage.Load(tiffStream.ToArray()))
            {
                image.SaveAsWebp(outputStream);
            }

            return await Task.FromResult(outputStream);
        }


        public async Task<MemoryStream> ConverTifToPNG(MemoryStream TifFile)
        {
            //using (var magicImage = new MagickImage(JPGfile))
            //{
            //    var memoryStream = new MemoryStream();
            //    magicImage.Format = MagickFormat.Jpeg;
            //    magicImage.Write(memoryStream);

            //    return memoryStream;
            //}
            var msPNG = new MemoryStream();

            return await Task.FromResult(msPNG);
        }

        public async Task<MemoryStream> ConvertMP3ToWav(MemoryStream MP3Stream)
        {
            MemoryStream ConvertedWaveStream = new MemoryStream();


            return await Task.FromResult(ConvertedWaveStream);
        }

        public async Task<MemoryStream> ConvertCSVToExcel(MemoryStream CSVStream)
        {
            ExcelTextFormat format = new ExcelTextFormat();
            format.Delimiter = ',';
            format.Encoding = new UTF8Encoding();
            format.EOL = "\n";

            var csvFile= Encoding.ASCII.GetString(CSVStream.ToArray());

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sheet1");

                worksheet.Cells["A1"].LoadFromText(csvFile, format);

                return await Task.FromResult(new MemoryStream(package.GetAsByteArray()));
            }
        }

        public async Task<MemoryStream> ConvertXLSXToCSV(MemoryStream XLSXStream)
        {
            using var package = new ExcelPackage(XLSXStream);
            var worksheet = package.Workbook.Worksheets[0];

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                var rowCount = worksheet.Dimension?.Rows ?? 0;
                var colCount = worksheet.Dimension?.Columns ?? 0;

                for (int row = 1; row <= rowCount; row++)
                {
                    var rowValues = new List<string>();
                    for (int col = 1; col <= colCount; col++)
                    {
                        var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                        rowValues.Add(EscapeCsvField(cellValue));
                    }
                    writer.WriteLine(string.Join(",", rowValues));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertJSONToXML(MemoryStream JSONStream)
        {
            var jsonString = Encoding.UTF8.GetString(JSONStream.ToArray());
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            var xmlRoot = new XElement("Root");
            ConvertJsonElementToXml(root, xmlRoot);

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(xmlRoot.ToString());
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertXMLToJSON(MemoryStream XMLStream)
        {
            var xmlString = Encoding.UTF8.GetString(XMLStream.ToArray());
            var xdoc = XDocument.Parse(xmlString);

            var jsonResult = ConvertXmlElementToJson(xdoc.Root);

            return await WriteStringToStreamAsync(JsonSerializer.Serialize(jsonResult, CachedJsonOptions));
        }

        private Dictionary<string, object> ConvertXmlElementToJson(XElement element)
        {
            var result = new Dictionary<string, object>();

            if (element == null)
                return result;

            // If element has no child elements, return its value directly
            if (!element.HasElements)
            {
                result[element.Name.LocalName] = element.Value;
                return result;
            }

            // Group child elements by name to handle arrays
            var childGroups = element.Elements().GroupBy(e => e.Name.LocalName);

            foreach (var group in childGroups)
            {
                var childElements = group.ToList();

                if (childElements.Count == 1)
                {
                    // Single element
                    var child = childElements[0];
                    if (child.HasElements)
                    {
                        result[group.Key] = ConvertXmlElementToJson(child);
                    }
                    else
                    {
                        result[group.Key] = child.Value;
                    }
                }
                else
                {
                    // Multiple elements - treat as array
                    var array = new List<object>();
                    foreach (var child in childElements)
                    {
                        if (child.HasElements)
                        {
                            array.Add(ConvertXmlElementToJson(child));
                        }
                        else
                        {
                            array.Add(child.Value);
                        }
                    }
                    result[group.Key] = array;
                }
            }

            return result;
        }

        public async Task<MemoryStream> ConvertMarkdownToHTML(MemoryStream MarkdownStream)
        {
            var markdownContent = Encoding.UTF8.GetString(MarkdownStream.ToArray());
            var htmlContent = Markdown.ToHtml(markdownContent, CachedMarkdownPipeline);

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(htmlContent);
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertYAMLToJSON(MemoryStream YAMLStream)
        {
            var yamlContent = Encoding.UTF8.GetString(YAMLStream.ToArray());
            var yamlObject = CachedYamlDeserializer.Deserialize(yamlContent);
            return await WriteStringToStreamAsync(JsonSerializer.Serialize(yamlObject, CachedJsonOptions));
        }

        public async Task<MemoryStream> ConvertJSONToYAML(MemoryStream JSONStream)
        {
            var jsonContent = Encoding.UTF8.GetString(JSONStream.ToArray());

            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            var yamlObject = ConvertJsonElementToYamlObject(root);
            var yamlContent = CachedYamlSerializer.Serialize(yamlObject);

            return await WriteStringToStreamAsync(yamlContent);
        }

        private object ConvertJsonElementToYamlObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ConvertJsonElementToYamlObject(property.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementToYamlObject(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString() ?? string.Empty;

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    if (element.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    return element.ToString();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.ToString();
            }
        }

        public async Task<MemoryStream> ConvertXLSXToJSON(MemoryStream XLSXStream)
        {
            using var package = new ExcelPackage(XLSXStream);
            var worksheet = package.Workbook.Worksheets[0];

            var rowCount = worksheet.Dimension?.Rows ?? 0;
            var colCount = worksheet.Dimension?.Columns ?? 0;

            if (rowCount < 2 || colCount == 0)
            {
                return await WriteStringToStreamAsync("[]");
            }

            // Get headers from first row
            var headers = new List<string>();
            for (int col = 1; col <= colCount; col++)
            {
                headers.Add(worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}");
            }

            // Convert rows to list of dictionaries
            var rows = new List<Dictionary<string, object>>();
            for (int row = 2; row <= rowCount; row++)
            {
                var rowData = new Dictionary<string, object>();
                for (int col = 1; col <= colCount; col++)
                {
                    var header = headers[col - 1];
                    var cellValue = worksheet.Cells[row, col].Value;

                    if (cellValue == null)
                    {
                        rowData[header] = null;
                    }
                    else if (cellValue is double doubleValue)
                    {
                        // Check if it's actually an integer
                        if (doubleValue == Math.Truncate(doubleValue))
                        {
                            rowData[header] = (long)doubleValue;
                        }
                        else
                        {
                            rowData[header] = doubleValue;
                        }
                    }
                    else if (cellValue is bool boolValue)
                    {
                        rowData[header] = boolValue;
                    }
                    else if (cellValue is DateTime dateTimeValue)
                    {
                        rowData[header] = dateTimeValue.ToString("o");
                    }
                    else
                    {
                        rowData[header] = cellValue.ToString();
                    }
                }
                rows.Add(rowData);
            }

            return await WriteStringToStreamAsync(JsonSerializer.Serialize(rows, CachedJsonOptions));
        }

        public async Task<MemoryStream> ConvertTSVToCSV(MemoryStream TSVStream)
        {
            var tsvContent = Encoding.UTF8.GetString(TSVStream.ToArray());

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                using var reader = new StringReader(tsvContent);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split('\t');
                    var csvFields = fields.Select(EscapeCsvField);
                    writer.WriteLine(string.Join(",", csvFields));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertCSVToJSON(MemoryStream CSVStream)
        {
            var csvContent = Encoding.UTF8.GetString(CSVStream.ToArray());
            using var reader = new StringReader(csvContent);

            // Read header line
            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
            {
                return await WriteStringToStreamAsync("[]");
            }

            var headers = ParseCsvLine(headerLine);
            var rows = new List<Dictionary<string, object>>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line);
                var rowData = new Dictionary<string, object>();

                for (int i = 0; i < headers.Count; i++)
                {
                    var value = i < values.Count ? values[i] : string.Empty;
                    rowData[headers[i]] = ConvertCsvValueToJson(value);
                }

                rows.Add(rowData);
            }

            return await WriteStringToStreamAsync(JsonSerializer.Serialize(rows, CachedJsonOptions));
        }

        public async Task<MemoryStream> ConvertJSONToCSV(MemoryStream JSONStream)
        {
            var jsonContent = Encoding.UTF8.GetString(JSONStream.ToArray());
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                return await WriteStringToStreamAsync(string.Empty);
            }

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                // Get headers from first object
                var firstItem = root[0];
                var headers = new List<string>();
                foreach (var property in firstItem.EnumerateObject())
                {
                    headers.Add(property.Name);
                }
                writer.WriteLine(string.Join(",", headers.Select(EscapeCsvField)));

                // Write each row
                foreach (var item in root.EnumerateArray())
                {
                    var values = new List<string>();
                    foreach (var header in headers)
                    {
                        if (item.TryGetProperty(header, out var prop))
                        {
                            values.Add(EscapeCsvField(ConvertJsonElementToCsvValue(prop)));
                        }
                        else
                        {
                            values.Add(string.Empty);
                        }
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertHTMLToText(MemoryStream HTMLStream)
        {
            var htmlContent = Encoding.UTF8.GetString(HTMLStream.ToArray());

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Remove script and style elements
            var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
            if (scriptNodes != null)
            {
                foreach (var node in scriptNodes)
                {
                    node.Remove();
                }
            }

            // Get text content with proper spacing for block elements
            var textContent = ExtractTextFromHtmlNode(doc.DocumentNode);

            // Clean up whitespace
            textContent = MultipleBlankLinesRegex.Replace(textContent, "\n\n");
            textContent = HorizontalWhitespaceRegex.Replace(textContent, " ");
            textContent = textContent.Trim();

            return await WriteStringToStreamAsync(textContent);
        }

        public async Task<MemoryStream> ConvertTSVToJSON(MemoryStream TSVStream)
        {
            var tsvContent = Encoding.UTF8.GetString(TSVStream.ToArray());
            using var reader = new StringReader(tsvContent);

            // Read header line
            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
            {
                return await WriteStringToStreamAsync("[]");
            }

            var headers = headerLine.Split('\t');
            var rows = new List<Dictionary<string, object>>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = line.Split('\t');
                var rowData = new Dictionary<string, object>();

                for (int i = 0; i < headers.Length; i++)
                {
                    var value = i < values.Length ? values[i] : string.Empty;
                    rowData[headers[i]] = ConvertCsvValueToJson(value);
                }

                rows.Add(rowData);
            }

            return await WriteStringToStreamAsync(JsonSerializer.Serialize(rows, CachedJsonOptions));
        }

        public async Task<MemoryStream> ConvertXMLToCSV(MemoryStream XMLStream)
        {
            var xmlString = Encoding.UTF8.GetString(XMLStream.ToArray());
            var xdoc = XDocument.Parse(xmlString);

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                // Find all leaf elements (elements with no child elements) at a consistent depth
                var rows = xdoc.Root?.Elements().ToList() ?? new List<XElement>();

                if (rows.Count == 0)
                {
                    return await WriteStringToStreamAsync(string.Empty);
                }

                // Get all unique element names from the first row to use as headers
                var headers = new HashSet<string>();
                foreach (var row in rows)
                {
                    foreach (var element in row.Elements())
                    {
                        headers.Add(element.Name.LocalName);
                    }
                }
                var headerList = headers.ToList();

                // Write header line
                writer.WriteLine(string.Join(",", headerList.Select(EscapeCsvField)));

                // Write data rows
                foreach (var row in rows)
                {
                    var values = new List<string>();
                    foreach (var header in headerList)
                    {
                        var element = row.Element(header);
                        values.Add(EscapeCsvField(element?.Value ?? string.Empty));
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertCSVToYAML(MemoryStream CSVStream)
        {
            var csvContent = Encoding.UTF8.GetString(CSVStream.ToArray());
            using var reader = new StringReader(csvContent);

            // Read header line
            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
            {
                return await WriteStringToStreamAsync(string.Empty);
            }

            var headers = ParseCsvLine(headerLine);
            var rows = new List<Dictionary<string, object>>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line);
                var rowData = new Dictionary<string, object>();

                for (int i = 0; i < headers.Count; i++)
                {
                    var value = i < values.Count ? values[i] : string.Empty;
                    rowData[headers[i]] = ConvertCsvValueToJson(value);
                }

                rows.Add(rowData);
            }

            var yamlContent = CachedYamlSerializer.Serialize(rows);
            return await WriteStringToStreamAsync(yamlContent);
        }

        /// <summary>
        /// Converts XML content to YAML format.
        /// Parses the XML structure and serializes it to YAML while preserving the hierarchy.
        /// </summary>
        /// <param name="XMLStream">The XML stream to convert</param>
        /// <returns>A YAML stream containing the converted content</returns>
        public async Task<MemoryStream> ConvertXMLToYAML(MemoryStream XMLStream)
        {
            // Security: Validate input size to prevent memory exhaustion
            if (XMLStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input XML exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            var xmlString = Encoding.UTF8.GetString(XMLStream.ToArray());
            var xdoc = XDocument.Parse(xmlString);

            if (xdoc.Root == null)
            {
                return await WriteStringToStreamAsync(string.Empty);
            }

            // Convert XML to dictionary structure (reusing existing XML to JSON logic)
            var jsonResult = ConvertXmlElementToJson(xdoc.Root);

            // Serialize to YAML
            var yamlContent = CachedYamlSerializer.Serialize(jsonResult);
            return await WriteStringToStreamAsync(yamlContent);
        }

        /// <summary>
        /// Converts YAML content to XML format.
        /// Deserializes the YAML to an object structure, then converts to XML.
        /// </summary>
        /// <param name="YAMLStream">The YAML stream to convert</param>
        /// <returns>An XML stream containing the converted content</returns>
        public async Task<MemoryStream> ConvertYAMLToXML(MemoryStream YAMLStream)
        {
            // Security: Validate input size to prevent memory exhaustion
            if (YAMLStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input YAML exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            var yamlContent = Encoding.UTF8.GetString(YAMLStream.ToArray());
            var yamlObject = CachedYamlDeserializer.Deserialize(yamlContent);

            var rootElement = new XElement("Root");
            ConvertObjectToXml(yamlObject, rootElement);

            var xmlString = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>{Environment.NewLine}{rootElement}";
            return await WriteStringToStreamAsync(xmlString);
        }

        /// <summary>
        /// Recursively converts an object to XML elements.
        /// Handles dictionaries, lists, and primitive values.
        /// </summary>
        private void ConvertObjectToXml(object obj, XElement parent)
        {
            if (obj == null)
                return;

            if (obj is Dictionary<object, object> dict)
            {
                foreach (var kvp in dict)
                {
                    var element = new XElement(kvp.Key.ToString() ?? "Item");
                    ConvertObjectToXml(kvp.Value, element);
                    parent.Add(element);
                }
            }
            else if (obj is IList<object> list)
            {
                foreach (var item in list)
                {
                    var element = new XElement("Item");
                    ConvertObjectToXml(item, element);
                    parent.Add(element);
                }
            }
            else
            {
                parent.Value = obj.ToString() ?? string.Empty;
            }
        }

        public Task<MemoryStream> ConvertImageToIco(MemoryStream imageStream)
        {
            var outputStream = new MemoryStream();
            imageStream.Position = 0;

            using (var image = ImageSharpImage.Load(imageStream))
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

        public Task<MemoryStream> ConvertSvgToPng(MemoryStream svgStream)
            => ConvertSvgToRaster(svgStream, SKEncodedImageFormat.Png, SKColors.Transparent, 100);

        public Task<MemoryStream> ConvertSvgToJpg(MemoryStream svgStream)
            => ConvertSvgToRaster(svgStream, SKEncodedImageFormat.Jpeg, SKColors.White, 80);

        public Task<MemoryStream> ConvertSvgToWebP(MemoryStream svgStream)
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

        private string ExtractTextFromHtmlNode(HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                return node.InnerText;
            }

            if (node.NodeType == HtmlNodeType.Comment)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var child in node.ChildNodes)
            {
                sb.Append(ExtractTextFromHtmlNode(child));
            }

            // Add line breaks for block elements
            if (BlockElements.Contains(node.Name))
            {
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            fields.Add(currentField.ToString());
            return fields;
        }

        private object ConvertCsvValueToJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            // Try to parse as number using invariant culture for consistent parsing
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                return longValue;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                return doubleValue;

            // Try to parse as boolean
            if (bool.TryParse(value, out var boolValue))
                return boolValue;

            return value;
        }

        private string ConvertJsonElementToCsvValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => element.ToString()
            };
        }

        private void ConvertJsonElementToXml(JsonElement jsonElement, XElement parent)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in jsonElement.EnumerateObject())
                    {
                        var element = new XElement(property.Name);
                        parent.Add(element);
                        ConvertJsonElementToXml(property.Value, element);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        var element = new XElement("Item");
                        parent.Add(element);
                        ConvertJsonElementToXml(item, element);
                    }
                    break;

                case JsonValueKind.String:
                    parent.Value = jsonElement.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Number:
                    parent.Value = jsonElement.ToString();
                    break;

                case JsonValueKind.True:
                    parent.Value = "true";
                    break;

                case JsonValueKind.False:
                    parent.Value = "false";
                    break;

                case JsonValueKind.Null:
                    parent.Value = string.Empty;
                    break;
            }
        }

        public IImmutableList<ConvertorDetails> GetConvertorsForFile(string inputFileName)
        {
            return Convertors.Where(cd => cd.ExtensionToConvert == Path.GetExtension(inputFileName)).ToImmutableList();
        }

        public IImmutableList<ConvertorDetails> GetAllAvailableConvertors()
        {
            return Convertors;
        }

        #region Archive Conversion Methods

        /// <summary>
        /// Decompresses a GZip (.gz or .tgz) file to extract the underlying TAR archive.
        /// </summary>
        public Task<MemoryStream> ConvertGzToTar(MemoryStream gzStream)
        {
            var outputStream = new MemoryStream();
            gzStream.Position = 0;

            using (var gzipStream = new GZipInputStream(gzStream))
            {
                var buffer = new byte[StreamBufferSize];

                // Security: Track total bytes written to prevent decompression bombs
                long totalBytesWritten = 0;
                int bytesRead;
                while ((bytesRead = gzipStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Security: Check cumulative size to prevent decompression bombs
                    totalBytesWritten += bytesRead;
                    if (totalBytesWritten > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    outputStream.Write(buffer, 0, bytesRead);
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Compresses a TAR archive using GZip compression (.tar.gz or .tgz).
        /// </summary>
        public Task<MemoryStream> ConvertTarToGz(MemoryStream tarStream)
        {
            var outputStream = new MemoryStream();
            tarStream.Position = 0;

            using (var gzipStream = new GZipOutputStream(outputStream))
            {
                gzipStream.IsStreamOwner = false;
                var buffer = new byte[StreamBufferSize];
                StreamUtils.Copy(tarStream, gzipStream, buffer);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Decompresses a BZip2 (.bz2 or .tbz2) file to extract the underlying TAR archive.
        /// </summary>
        public Task<MemoryStream> ConvertBz2ToTar(MemoryStream bz2Stream)
        {
            var outputStream = new MemoryStream();
            bz2Stream.Position = 0;

            using (var bzip2Stream = new ICSharpCode.SharpZipLib.BZip2.BZip2InputStream(bz2Stream))
            {
                var buffer = new byte[StreamBufferSize];

                // Security: Track total bytes written to prevent decompression bombs
                long totalBytesWritten = 0;
                int bytesRead;
                while ((bytesRead = bzip2Stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Security: Check cumulative size to prevent decompression bombs
                    totalBytesWritten += bytesRead;
                    if (totalBytesWritten > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    outputStream.Write(buffer, 0, bytesRead);
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a ZIP archive to TAR format.
        /// Extracts all entries from the ZIP and repackages them into a TAR archive.
        /// </summary>
        public Task<MemoryStream> ConvertZipToTar(MemoryStream zipStream)
        {
            var outputStream = new MemoryStream();
            zipStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var zipFile = new ZipFile(zipStream))
            using (var tarOutputStream = new TarOutputStream(outputStream, System.Text.Encoding.UTF8))
            {
                tarOutputStream.IsStreamOwner = false;

                var entries = new List<ZipEntry>();
                foreach (ZipEntry zipEntry in zipFile)
                {
                    if (!zipEntry.IsDirectory)
                        entries.Add(zipEntry);
                }

                // Security: Check entry count to prevent decompression bombs
                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var zipEntry in entries)
                {
                    // Security: Check entry size to prevent decompression bombs
                    if (zipEntry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{zipEntry.Name}' exceeds maximum allowed size");

                    // Security: Track cumulative size to prevent decompression bombs
                    totalExtractedSize += zipEntry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    // Security: Sanitize entry path to prevent path traversal
                    var sanitizedName = SanitizeArchiveEntryPath(zipEntry.Name);

                    var tarEntry = TarEntry.CreateTarEntry(sanitizedName);
                    tarEntry.Size = zipEntry.Size;

                    if (zipEntry.DateTime != DateTime.MinValue)
                    {
                        tarEntry.ModTime = zipEntry.DateTime;
                    }

                    tarOutputStream.PutNextEntry(tarEntry);

                    using (var zipInputStream = zipFile.GetInputStream(zipEntry))
                    {
                        StreamUtils.Copy(zipInputStream, tarOutputStream, buffer);
                    }

                    tarOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a TAR archive to ZIP format.
        /// Extracts all entries from the TAR and repackages them into a ZIP archive.
        /// </summary>
        public Task<MemoryStream> ConvertTarToZip(MemoryStream tarStream)
        {
            var outputStream = new MemoryStream();
            tarStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;
            int entryCount = 0;

            using (var tarInputStream = new TarInputStream(tarStream, System.Text.Encoding.UTF8))
            using (var zipOutputStream = new ZipOutputStream(outputStream))
            {
                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.SetLevel(DefaultZipCompressionLevel);

                TarEntry tarEntry;
                while ((tarEntry = tarInputStream.GetNextEntry()) != null)
                {
                    if (tarEntry.IsDirectory)
                        continue;

                    // Security: Check entry count to prevent decompression bombs
                    entryCount++;
                    if (entryCount > MaxEntryCount)
                        throw new InvalidOperationException("Archive contains too many entries");

                    // Security: Check entry size to prevent decompression bombs
                    if (tarEntry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{tarEntry.Name}' exceeds maximum allowed size");

                    // Security: Track cumulative size to prevent decompression bombs
                    totalExtractedSize += tarEntry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    // Security: Sanitize entry path to prevent path traversal
                    var sanitizedName = SanitizeArchiveEntryPath(tarEntry.Name);

                    var zipEntry = new ZipEntry(sanitizedName)
                    {
                        DateTime = tarEntry.ModTime,
                        Size = tarEntry.Size
                    };

                    zipOutputStream.PutNextEntry(zipEntry);
                    StreamUtils.Copy(tarInputStream, zipOutputStream, buffer);
                    zipOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a 7z archive to ZIP format.
        /// Extracts all entries from the 7z and repackages them into a ZIP archive.
        /// </summary>
        public Task<MemoryStream> Convert7zToZip(MemoryStream sevenZipStream)
        {
            var outputStream = new MemoryStream();
            sevenZipStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = SevenZipArchive.Open(sevenZipStream))
            using (var zipOutputStream = new ZipOutputStream(outputStream))
            {
                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.SetLevel(DefaultZipCompressionLevel);

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                // Security: Check entry count to prevent decompression bombs
                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    // Security: Check entry size to prevent decompression bombs
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    // Security: Track cumulative size to prevent decompression bombs
                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    // Security: Sanitize entry path to prevent path traversal
                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var zipEntry = new ZipEntry(sanitizedName)
                    {
                        DateTime = entry.CreatedTime ?? DateTime.Now,
                        Size = entry.Size
                    };

                    zipOutputStream.PutNextEntry(zipEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, zipOutputStream, buffer);
                    }

                    zipOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a 7z archive to TAR format.
        /// Extracts all entries from the 7z and repackages them into a TAR archive.
        /// </summary>
        public Task<MemoryStream> Convert7zToTar(MemoryStream sevenZipStream)
        {
            var outputStream = new MemoryStream();
            sevenZipStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = SevenZipArchive.Open(sevenZipStream))
            using (var tarOutputStream = new TarOutputStream(outputStream, System.Text.Encoding.UTF8))
            {
                tarOutputStream.IsStreamOwner = false;

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                // Security: Check entry count to prevent decompression bombs
                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    // Security: Check entry size to prevent decompression bombs
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    // Security: Track cumulative size to prevent decompression bombs
                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    // Security: Sanitize entry path to prevent path traversal
                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var tarEntry = TarEntry.CreateTarEntry(sanitizedName);
                    tarEntry.Size = entry.Size;

                    if (entry.CreatedTime.HasValue)
                    {
                        tarEntry.ModTime = entry.CreatedTime.Value;
                    }

                    tarOutputStream.PutNextEntry(tarEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, tarOutputStream, buffer);
                    }

                    tarOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a RAR archive to ZIP format.
        /// Extracts all entries from the RAR and repackages them into a ZIP archive.
        /// </summary>
        public Task<MemoryStream> ConvertRarToZip(MemoryStream rarStream)
        {
            var outputStream = new MemoryStream();
            rarStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = RarArchive.Open(rarStream))
            using (var zipOutputStream = new ZipOutputStream(outputStream))
            {
                zipOutputStream.IsStreamOwner = false;
                zipOutputStream.SetLevel(DefaultZipCompressionLevel);

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                // Security: Check entry count to prevent decompression bombs
                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    // Security: Check entry size to prevent decompression bombs
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    // Security: Track cumulative size to prevent decompression bombs
                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    // Security: Sanitize entry path to prevent path traversal
                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var zipEntry = new ZipEntry(sanitizedName)
                    {
                        DateTime = entry.CreatedTime ?? DateTime.Now,
                        Size = entry.Size
                    };

                    zipOutputStream.PutNextEntry(zipEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, zipOutputStream, buffer);
                    }

                    zipOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a RAR archive to TAR format.
        /// Extracts all entries from the RAR and repackages them into a TAR archive.
        /// </summary>
        public Task<MemoryStream> ConvertRarToTar(MemoryStream rarStream)
        {
            var outputStream = new MemoryStream();
            rarStream.Position = 0;
            var buffer = new byte[StreamBufferSize];
            long totalExtractedSize = 0;

            using (var archive = RarArchive.Open(rarStream))
            using (var tarOutputStream = new TarOutputStream(outputStream, System.Text.Encoding.UTF8))
            {
                tarOutputStream.IsStreamOwner = false;

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                // Security: Check entry count to prevent decompression bombs
                if (entries.Count > MaxEntryCount)
                    throw new InvalidOperationException("Archive contains too many entries");

                foreach (var entry in entries)
                {
                    // Security: Check entry size to prevent decompression bombs
                    if (entry.Size > MaxUncompressedSize)
                        throw new InvalidOperationException($"Entry '{entry.Key}' exceeds maximum allowed size");

                    // Security: Track cumulative size to prevent decompression bombs
                    totalExtractedSize += entry.Size;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new InvalidOperationException("Total uncompressed size exceeds maximum allowed");

                    // Security: Sanitize entry path to prevent path traversal
                    var sanitizedName = SanitizeArchiveEntryPath(entry.Key);

                    var tarEntry = TarEntry.CreateTarEntry(sanitizedName);
                    tarEntry.Size = entry.Size;

                    if (entry.CreatedTime.HasValue)
                    {
                        tarEntry.ModTime = entry.CreatedTime.Value;
                    }

                    tarOutputStream.PutNextEntry(tarEntry);

                    using (var entryStream = entry.OpenEntryStream())
                    {
                        StreamUtils.Copy(entryStream, tarOutputStream, buffer);
                    }

                    tarOutputStream.CloseEntry();
                }
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region JPEG 2000 Conversion Methods

        /// <summary>
        /// Converts a JPEG 2000 (JP2/J2K) image to PNG format.
        /// </summary>
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

        /// <summary>
        /// Converts a JPEG 2000 (JP2/J2K) image to JPG format.
        /// </summary>
        public Task<MemoryStream> ConvertJp2ToJpg(MemoryStream jp2Stream)
        {
            var outputStream = new MemoryStream();
            jp2Stream.Position = 0;

            var decodedImage = J2kImage.FromStream(jp2Stream);
            using (var image = decodedImage.As<Image<Rgb24>>())
            {
                image.SaveAsJpeg(outputStream, CachedJpegEncoder80);
            }

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Converts a JPEG 2000 (JP2/J2K) image to WebP format.
        /// </summary>
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

        #region PDF Conversion Methods

        /// <summary>
        /// Converts an image to PDF format using QuestPDF.
        /// Supports PNG, JPG, GIF, BMP, and WebP formats.
        /// </summary>
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

        #endregion

        #region PDF Merge/Split Methods

        /// <summary>
        /// Merges multiple PDF files into a single PDF document.
        /// Note: This method requires multiple input streams and is designed for special UI handling.
        /// </summary>
        /// <param name="pdfStreams">List of PDF streams to merge</param>
        /// <returns>A single merged PDF stream</returns>
        public Task<MemoryStream> MergePdfsAsync(List<MemoryStream> pdfStreams)
        {
            if (pdfStreams == null || pdfStreams.Count == 0)
            {
                throw new ArgumentException("No PDF streams provided for merging");
            }

            // Reset all stream positions
            foreach (var stream in pdfStreams)
            {
                stream.Position = 0;
            }

            // Create temporary files for PdfMerger (it requires file paths)
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

                // Use PdfMerger to merge the files
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
                // Clean up temporary files
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

        /// <summary>
        /// Splits a single PDF into individual page PDFs.
        /// Note: This method returns multiple outputs and is designed for special UI handling.
        /// </summary>
        /// <param name="pdfStream">The PDF stream to split</param>
        /// <returns>A list of MemoryStreams, one for each page</returns>
        public Task<List<MemoryStream>> SplitPdfAsync(MemoryStream pdfStream)
        {
            var resultStreams = new List<MemoryStream>();
            pdfStream.Position = 0;

            using (var document = PdfDocument.Open(pdfStream))
            {
                for (int i = 0; i < document.NumberOfPages; i++)
                {
                    var pageNumber = i + 1;
                    var builder = new PdfDocumentBuilder();
                    builder.AddPage(document, pageNumber);

                    var pageBytes = builder.Build();
                    var pageStream = new MemoryStream(pageBytes);
                    pageStream.Position = 0;
                    resultStreams.Add(pageStream);
                }
            }

            return Task.FromResult(resultStreams);
        }

        /// <summary>
        /// Extracts a single page from a PDF.
        /// </summary>
        /// <param name="pdfStream">The PDF stream</param>
        /// <param name="pageNumber">The 1-based page number to extract</param>
        /// <returns>A PDF stream containing only the specified page</returns>
        public Task<MemoryStream> ExtractPdfPageAsync(MemoryStream pdfStream, int pageNumber)
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

        #endregion

        #region QR Code Conversion Methods

        /// <summary>
        /// Converts text content from a stream to a QR code PNG image.
        /// Reads text from the input stream and generates a QR code.
        /// </summary>
        public Task<MemoryStream> ConvertTextToQrCodePng(MemoryStream textStream)
        {
            textStream.Position = 0;
            var textContent = Encoding.UTF8.GetString(textStream.ToArray()).Trim();

            if (string.IsNullOrEmpty(textContent))
            {
                throw new ArgumentException("Input text is empty");
            }

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

        #endregion

        #region Barcode Conversion Methods

        /// <summary>
        /// Converts text content from a stream to a Code128 barcode JPG image.
        /// Reads text from the input stream and generates a Code128 barcode.
        /// </summary>
        public Task<MemoryStream> ConvertTextToBarcodeJpg(MemoryStream textStream)
        {
            textStream.Position = 0;
            var textContent = Encoding.UTF8.GetString(textStream.ToArray()).Trim();

            if (string.IsNullOrEmpty(textContent))
            {
                throw new ArgumentException("Input text is empty");
            }

            var writer = new BarcodeWriter<SkiaSharp.SKBitmap>
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
            using (var image = SkiaSharp.SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90))
            {
                var outputStream = new MemoryStream();
                data.SaveTo(outputStream);
                outputStream.Position = 0;
                return Task.FromResult(outputStream);
            }
        }

        #endregion

        #region PDF to Text Conversion Methods

        /// <summary>
        /// Extracts text content from a PDF document.
        /// Reads all pages and concatenates the text content.
        /// </summary>
        /// <param name="pdfStream">The PDF stream to extract text from</param>
        /// <returns>A text stream containing the extracted text</returns>
        public async Task<MemoryStream> ConvertPdfToText(MemoryStream pdfStream)
        {
            pdfStream.Position = 0;

            using (var document = PdfDocument.Open(pdfStream))
            {
                var textBuilder = new System.Text.StringBuilder();

                foreach (var page in document.GetPages())
                {
                    var pageText = page.Text;
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        textBuilder.AppendLine(pageText);
                        textBuilder.AppendLine(); // Add blank line between pages
                    }
                }

                var extractedText = textBuilder.ToString().Trim();
                return await WriteStringToStreamAsync(extractedText);
            }
        }

        #endregion

        #region Markdown to PDF Conversion Methods

        /// <summary>
        /// Converts Markdown content to PDF format.
        /// Parses markdown to HTML using Markdig, then renders to PDF using QuestPDF.
        /// </summary>
        /// <param name="markdownStream">The markdown stream to convert</param>
        /// <returns>A PDF stream containing the rendered markdown content</returns>
        public Task<MemoryStream> ConvertMarkdownToPdf(MemoryStream markdownStream)
        {
            markdownStream.Position = 0;
            var markdownContent = Encoding.UTF8.GetString(markdownStream.ToArray());

            if (string.IsNullOrWhiteSpace(markdownContent))
            {
                throw new ArgumentException("Markdown content is empty");
            }

            // Convert markdown to HTML
            var htmlContent = Markdown.ToHtml(markdownContent, CachedMarkdownPipeline);

            // Extract text from HTML for PDF rendering
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Remove script and style elements
            var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
            if (scriptNodes != null)
            {
                foreach (var node in scriptNodes)
                {
                    node.Remove();
                }
            }

            // Get text content
            var textContent = ExtractTextFromHtmlNode(doc.DocumentNode);

            // Clean up whitespace
            textContent = MultipleBlankLinesRegex.Replace(textContent, "\n\n");
            textContent = HorizontalWhitespaceRegex.Replace(textContent, " ");
            textContent = textContent.Trim();

            // Create PDF using QuestPDF
            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        #endregion

        #region EPUB Conversion Methods

        /// <summary>
        /// Converts an EPUB ebook to PDF format.
        /// Extracts text content from all chapters and renders to PDF.
        /// </summary>
        /// <param name="epubStream">The EPUB stream to convert</param>
        /// <returns>A PDF stream containing the ebook content</returns>
        public async Task<MemoryStream> ConvertEpubToPdf(MemoryStream epubStream)
        {
            epubStream.Position = 0;

            // Write EPUB bytes to a temporary file since EpubReader requires a file path
            var epubBytes = epubStream.ToArray();
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"epub_{Guid.NewGuid()}.epub");

            try
            {
                await File.WriteAllBytesAsync(tempFilePath, epubBytes);
                var book = await EpubReader.ReadBookAsync(tempFilePath);

                var textBuilder = new StringBuilder();

                // Iterate through the reading order (spine) of the book
                foreach (var chapterFile in book.ReadingOrder)
                {
                    var chapterContent = chapterFile.Content;

                    // Parse HTML content to extract text
                    var doc = new HtmlDocument();
                    doc.LoadHtml(chapterContent);

                    // Remove script and style elements
                    var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
                    if (scriptNodes != null)
                    {
                        foreach (var node in scriptNodes)
                        {
                            node.Remove();
                        }
                    }

                    // Get text content with proper spacing
                    var chapterText = ExtractTextFromHtmlNode(doc.DocumentNode);

                    // Clean up whitespace
                    chapterText = MultipleBlankLinesRegex.Replace(chapterText, "\n\n");
                    chapterText = HorizontalWhitespaceRegex.Replace(chapterText, " ");
                    chapterText = chapterText.Trim();

                    if (!string.IsNullOrWhiteSpace(chapterText))
                    {
                        textBuilder.AppendLine(chapterText);
                        textBuilder.AppendLine();
                        textBuilder.AppendLine(); // Extra space between chapters
                    }
                }

                var fullText = textBuilder.ToString().Trim();

                if (string.IsNullOrWhiteSpace(fullText))
                {
                    throw new ArgumentException("EPUB content is empty or could not be extracted");
                }

                // Create PDF using QuestPDF
                var outputStream = new MemoryStream();

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Content().Text(fullText);
                    });
                }).GeneratePdf(outputStream);

                outputStream.Position = 0;
                return outputStream;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        /// <summary>
        /// Converts an EPUB ebook to plain text format.
        /// Extracts text content from all chapters.
        /// </summary>
        /// <param name="epubStream">The EPUB stream to convert</param>
        /// <returns>A text stream containing the ebook content</returns>
        public async Task<MemoryStream> ConvertEpubToTxt(MemoryStream epubStream)
        {
            epubStream.Position = 0;

            // Write EPUB bytes to a temporary file since EpubReader requires a file path
            var epubBytes = epubStream.ToArray();
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"epub_{Guid.NewGuid()}.epub");

            try
            {
                await File.WriteAllBytesAsync(tempFilePath, epubBytes);
                var book = await EpubReader.ReadBookAsync(tempFilePath);

                var textBuilder = new StringBuilder();

                // Iterate through the reading order (spine) of the book
                foreach (var chapterFile in book.ReadingOrder)
                {
                    var chapterContent = chapterFile.Content;

                    // Parse HTML content to extract text
                    var doc = new HtmlDocument();
                    doc.LoadHtml(chapterContent);

                    // Remove script and style elements
                    var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
                    if (scriptNodes != null)
                    {
                        foreach (var node in scriptNodes)
                        {
                            node.Remove();
                        }
                    }

                    // Get text content with proper spacing
                    var chapterText = ExtractTextFromHtmlNode(doc.DocumentNode);

                    // Clean up whitespace
                    chapterText = MultipleBlankLinesRegex.Replace(chapterText, "\n\n");
                    chapterText = HorizontalWhitespaceRegex.Replace(chapterText, " ");
                    chapterText = chapterText.Trim();

                    if (!string.IsNullOrWhiteSpace(chapterText))
                    {
                        textBuilder.AppendLine(chapterText);
                        textBuilder.AppendLine();
                        textBuilder.AppendLine(); // Extra space between chapters
                    }
                }

                var fullText = textBuilder.ToString().Trim();

                if (string.IsNullOrWhiteSpace(fullText))
                {
                    throw new ArgumentException("EPUB content is empty or could not be extracted");
                }

                return await WriteStringToStreamAsync(fullText);
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        #endregion

        // CSS styles for DOCX to HTML conversion
        private const string DocxToHtmlCss = @"
body { font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; }
h1 { font-size: 24px; margin-top: 24px; }
h2 { font-size: 20px; margin-top: 20px; }
h3 { font-size: 16px; margin-top: 16px; }
p { margin: 12px 0; }
table { border-collapse: collapse; width: 100%; margin: 16px 0; }
th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
th { background-color: #f2f2f2; }
ul, ol { margin: 12px 0; padding-left: 24px; }
li { margin: 4px 0; }";

        // Maximum column width in characters for XLSX to PDF conversion
        // Prevents excessively wide columns in PDF output
        private const int MaxColumnWidthChars = 50;

        #region DOCX Conversion Methods

        /// <summary>
        /// Converts a DOCX document to PDF format.
        /// Parses DOCX content using Open-XML-SDK and renders to PDF using QuestPDF.
        /// </summary>
        /// <param name="docxStream">The DOCX stream to convert</param>
        /// <returns>A PDF stream containing the rendered document content</returns>
        public async Task<MemoryStream> ConvertDocxToPdf(MemoryStream docxStream)
        {
            docxStream.Position = 0;

            var textContent = await ExtractTextFromDocxAsync(docxStream);

            if (string.IsNullOrWhiteSpace(textContent))
            {
                throw new ArgumentException("DOCX content is empty or could not be extracted");
            }

            // Create PDF using QuestPDF
            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Converts a DOCX document to HTML format.
        /// Parses DOCX content using Open-XML-SDK and converts to semantic HTML.
        /// </summary>
        /// <param name="docxStream">The DOCX stream to convert</param>
        /// <returns>An HTML stream containing the document content</returns>
        public async Task<MemoryStream> ConvertDocxToHtml(MemoryStream docxStream)
        {
            docxStream.Position = 0;

            var htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine("<!DOCTYPE html>");
            htmlBuilder.AppendLine("<html>");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("<meta charset=\"UTF-8\">");
            htmlBuilder.AppendLine("<style>");
            htmlBuilder.AppendLine(DocxToHtmlCss);
            htmlBuilder.AppendLine("</style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body>");

            // Create a copy of the stream with auto-grow enabled for WordprocessingDocument
            // This approach works in WebAssembly without file system access
            var docxCopy = new MemoryStream(docxStream.ToArray(), true);

            using var wordDoc = WordprocessingDocument.Open(docxCopy, false);
            var mainPart = wordDoc.MainDocumentPart;

            if (mainPart?.Document?.Body != null)
            {
                // Track list state for proper HTML list wrapping
                bool inList = false;
                var elements = mainPart.Document.Body.Elements().ToList();

                for (int i = 0; i < elements.Count; i++)
                {
                    var element = elements[i];
                    var isListItem = element is NumberingInstance ||
                                     element.InnerText?.StartsWith("\u2022") == true ||
                                     element.InnerText?.StartsWith("- ") == true;

                    if (isListItem && !inList)
                    {
                        htmlBuilder.AppendLine("<ul>");
                        inList = true;
                    }
                    else if (!isListItem && inList)
                    {
                        htmlBuilder.AppendLine("</ul>");
                        inList = false;
                    }

                    ProcessDocxElementToHtml(element, htmlBuilder);
                }

                // Close any open list at the end
                if (inList)
                {
                    htmlBuilder.AppendLine("</ul>");
                }
            }

            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            return await WriteStringToStreamAsync(htmlBuilder.ToString());
        }

        /// <summary>
        /// Extracts plain text from a DOCX document.
        /// </summary>
        private async Task<string> ExtractTextFromDocxAsync(MemoryStream docxStream)
        {
            var textBuilder = new StringBuilder();

            // Create a copy of the stream with auto-grow enabled for WordprocessingDocument
            // This approach works in WebAssembly without file system access
            var docxCopy = new MemoryStream(docxStream.ToArray(), true);

            using var wordDoc = WordprocessingDocument.Open(docxCopy, false);
            var mainPart = wordDoc.MainDocumentPart;

            if (mainPart?.Document?.Body != null)
            {
                foreach (var element in mainPart.Document.Body.Elements())
                {
                    var text = ExtractTextFromDocxElement(element);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textBuilder.AppendLine(text);
                    }
                }
            }

            return textBuilder.ToString().Trim();
        }

        /// <summary>
        /// Extracts text from a DOCX element (paragraph, table, etc.).
        /// </summary>
        private string ExtractTextFromDocxElement(OpenXmlElement element)
        {
            if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
            {
                return para.InnerText;
            }
            else if (element is Table table)
            {
                var tableText = new StringBuilder();
                foreach (var row in table.Elements<TableRow>())
                {
                    var rowTexts = new List<string>();
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        rowTexts.Add(cell.InnerText.Trim());
                    }
                    tableText.AppendLine(string.Join(" | ", rowTexts));
                }
                return tableText.ToString();
            }
            else
            {
                return element.InnerText;
            }
        }

        /// <summary>
        /// Processes a DOCX element and appends HTML representation to the builder.
        /// </summary>
        private void ProcessDocxElementToHtml(OpenXmlElement element, StringBuilder htmlBuilder)
        {
            if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
            {
                var text = para.InnerText;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                // Check for heading styles
                var styleId = GetParagraphStyleId(para);
                if (styleId != null)
                {
                    var headingTag = GetHeadingTag(styleId);
                    if (headingTag != null)
                    {
                        htmlBuilder.AppendLine($"<{headingTag}>{EscapeHtml(text)}</{headingTag}>");
                        return;
                    }
                }

                // Check for bold/italic runs
                var formattedText = FormatRuns(para);
                htmlBuilder.AppendLine($"<p>{formattedText}</p>");
            }
            else if (element is Table table)
            {
                htmlBuilder.AppendLine("<table>");
                var isFirstRow = true;
                foreach (var row in table.Elements<TableRow>())
                {
                    htmlBuilder.AppendLine("<tr>");
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        var tag = isFirstRow ? "th" : "td";
                        htmlBuilder.AppendLine($"<{tag}>{EscapeHtml(cell.InnerText)}</{tag}>");
                    }
                    htmlBuilder.AppendLine("</tr>");
                    isFirstRow = false;
                }
                htmlBuilder.AppendLine("</table>");
            }
            else if (element is NumberingInstance || element.InnerText?.StartsWith("\u2022") == true ||
                     element.InnerText?.StartsWith("- ") == true)
            {
                // Handle list items
                htmlBuilder.AppendLine($"<li>{EscapeHtml(element.InnerText)}</li>");
            }
        }

        /// <summary>
        /// Gets the style ID for a paragraph.
        /// </summary>
        private static string GetParagraphStyleId(DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
        {
            var props = para.ParagraphProperties;
            if (props != null)
            {
                var style = props.ParagraphStyleId;
                if (style != null)
                {
                    return style.Val?.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the HTML heading tag for a style ID, or null if not a heading.
        /// </summary>
        private static string GetHeadingTag(string styleId)
        {
            if (styleId.Contains("Heading1", StringComparison.OrdinalIgnoreCase)) return "h1";
            if (styleId.Contains("Heading2", StringComparison.OrdinalIgnoreCase)) return "h2";
            if (styleId.Contains("Heading3", StringComparison.OrdinalIgnoreCase)) return "h3";
            return null;
        }

        /// <summary>
        /// Formats runs within a paragraph, preserving bold and italic formatting.
        /// </summary>
        private static string FormatRuns(DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
        {
            var result = new StringBuilder();
            foreach (var run in para.Elements<Run>())
            {
                var text = run.InnerText;
                if (string.IsNullOrEmpty(text))
                    continue;

                var isBold = run.RunProperties?.Bold != null;
                var isItalic = run.RunProperties?.Italic != null;
                var isUnderline = run.RunProperties?.Underline != null;

                var formattedText = EscapeHtml(text);

                if (isUnderline)
                    formattedText = $"<u>{formattedText}</u>";
                if (isItalic)
                    formattedText = $"<em>{formattedText}</em>";
                if (isBold)
                    formattedText = $"<strong>{formattedText}</strong>";

                result.Append(formattedText);
            }
            return result.ToString();
        }

        /// <summary>
        /// Escapes special HTML characters.
        /// </summary>
        private static string EscapeHtml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        #endregion

        #region XLSX to PDF Conversion Methods

        /// <summary>
        /// Maximum number of rows to process in XLSX to PDF conversion.
        /// Prevents memory issues with large spreadsheets.
        /// </summary>
        private const int MaxRowsForXlsxToPdf = 500;

        /// <summary>
        /// Converts an XLSX spreadsheet to PDF format.
        /// Uses EPPlus to read the spreadsheet and QuestPDF to render as a table.
        /// </summary>
        /// <param name="xlsxStream">The XLSX stream to convert</param>
        /// <returns>A PDF stream containing the rendered spreadsheet content</returns>
        public async Task<MemoryStream> ConvertXlsxToPdf(MemoryStream xlsxStream)
        {
            xlsxStream.Position = 0;

            using var package = new ExcelPackage(xlsxStream);
            var worksheet = package.Workbook.Worksheets[0];

            var originalRowCount = worksheet.Dimension?.Rows ?? 0;
            var colCount = worksheet.Dimension?.Columns ?? 0;

            if (originalRowCount == 0 || colCount == 0)
            {
                throw new ArgumentException("XLSX spreadsheet is empty");
            }

            // Limit the number of rows to prevent memory issues
            var rowCount = Math.Min(originalRowCount, MaxRowsForXlsxToPdf);
            var wasTruncated = originalRowCount > MaxRowsForXlsxToPdf;

            // Extract data from worksheet
            var tableData = new List<List<string>>();
            var columnWidths = new int[colCount];

            for (int row = 1; row <= rowCount; row++)
            {
                var rowData = new List<string>();
                for (int col = 1; col <= colCount; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Text ?? string.Empty;
                    rowData.Add(cellValue);

                    // Track max column width for formatting
                    if (cellValue.Length > columnWidths[col - 1])
                    {
                        columnWidths[col - 1] = Math.Min(cellValue.Length, MaxColumnWidthChars);
                    }
                }
                tableData.Add(rowData);
            }

            // Create PDF using QuestPDF - render as text lines with proper formatting
            var outputStream = new MemoryStream();

            // Build text content for PDF
            var textBuilder = new StringBuilder();

            // Add truncation warning if applicable
            if (wasTruncated)
            {
                textBuilder.AppendLine($"WARNING: Document truncated to {MaxRowsForXlsxToPdf} of {originalRowCount} rows");
                textBuilder.AppendLine(new string('-', 80));
                textBuilder.AppendLine();
            }

            for (int row = 0; row < tableData.Count; row++)
            {
                var rowData = tableData[row];
                var line = string.Join(" | ", rowData.Select((cell, col) =>
                    cell.PadRight(columnWidths[col])));
                textBuilder.AppendLine(line);
                textBuilder.AppendLine(new string('-', line.Length > 80 ? 80 : line.Length));
            }

            var textContent = textBuilder.ToString();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(7).FontFamily(QuestPDF.Helpers.Fonts.CourierNew));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return await Task.FromResult(outputStream);
        }

        #endregion

        #region PPTX to PDF Conversion Methods
        /// <summary>
        /// Converts a PPTX (PowerPoint) presentation to PDF format.
        /// Uses DocumentFormat.OpenXml to parse PPTX and QuestPDF to render slides.
        /// Extracts text and basic layout from each slide.
        /// </summary>
        /// <param name="pptxStream">The PPTX stream to convert</param>
        /// <returns>A PDF stream containing the rendered presentation content</returns>
        public async Task<MemoryStream> ConvertPptxToPdf(MemoryStream pptxStream)
        {
            pptxStream.Position = 0;

            var slideTexts = new List<string>();

            // Create a copy of the stream with auto-grow enabled for PresentationDocument
            var pptxCopy = new MemoryStream(pptxStream.ToArray(), true);

            using var presentation = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(pptxCopy, false);
            var presentationPart = presentation.PresentationPart;

            if (presentationPart == null)
            {
                throw new ArgumentException("PPTX file has no presentation part");
            }

            // Get slide parts directly from the presentation part
            var slideParts = presentationPart.SlideParts;
            if (slideParts == null || !slideParts.Any())
            {
                throw new ArgumentException("PPTX presentation contains no slides");
            }

            foreach (var slidePart in slideParts)
            {
                if (slidePart?.Slide?.CommonSlideData?.ShapeTree != null)
                {
                    var slideText = ExtractTextFromSlide(slidePart.Slide.CommonSlideData.ShapeTree);
                    if (!string.IsNullOrWhiteSpace(slideText))
                    {
                        slideTexts.Add(slideText);
                    }
                }
            }

            if (slideTexts.Count == 0)
            {
                throw new ArgumentException("PPTX presentation contains no extractable text content");
            }

            // Build text content for PDF with slide separators
            var textBuilder = new StringBuilder();
            for (int i = 0; i < slideTexts.Count; i++)
            {
                textBuilder.AppendLine($"=== Slide {i + 1} ===");
                textBuilder.AppendLine(slideTexts[i]);
                textBuilder.AppendLine();
            }

            var textContent = textBuilder.ToString().Trim();

            // Create PDF using QuestPDF
            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return await Task.FromResult(outputStream);
        }

        /// <summary>
        /// Extracts text content from a slide's shape tree.
        /// Limits extraction to prevent unbounded memory allocation.
        /// </summary>
        private string ExtractTextFromSlide(DocumentFormat.OpenXml.Presentation.ShapeTree shapeTree)
        {
            var textBuilder = new StringBuilder();

            foreach (var shape in shapeTree.Elements<DocumentFormat.OpenXml.Presentation.Shape>())
            {
                // Check if we've reached the maximum text content length
                if (textBuilder.Length >= MaxTextContentLength)
                    break;

                var textBody = shape.TextBody;
                if (textBody != null)
                {
                    foreach (var paragraph in textBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>())
                    {
                        // Check limit before processing each paragraph
                        if (textBuilder.Length >= MaxTextContentLength)
                            break;

                        var paragraphText = new StringBuilder();
                        foreach (var run in paragraph.Elements<DocumentFormat.OpenXml.Drawing.Run>())
                        {
                            if (run.Text != null)
                            {
                                paragraphText.Append(run.Text.Text);
                            }
                        }

                        if (paragraphText.Length > 0)
                        {
                            textBuilder.AppendLine(paragraphText.ToString());
                        }
                    }
                }
            }

            return textBuilder.ToString().Trim();
        }

        #endregion

        #region HTML to PDF Conversion Methods

        /// <summary>
        /// Converts HTML content to PDF format.
        /// Uses HtmlAgilityPack to parse HTML and QuestPDF to render content.
        /// Supports basic HTML elements: p, h1-h6, ul, ol, li, table, img (base64), a, strong, em.
        /// </summary>
        /// <param name="htmlStream">The HTML stream to convert</param>
        /// <returns>A PDF stream containing the rendered HTML content</returns>
        public Task<MemoryStream> ConvertHtmlToPdf(MemoryStream htmlStream)
        {
            htmlStream.Position = 0;
            var htmlContent = Encoding.UTF8.GetString(htmlStream.ToArray());

            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                throw new ArgumentException("HTML content is empty");
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // Remove script and style elements
            var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
            if (scriptNodes != null)
            {
                foreach (var node in scriptNodes)
                {
                    node.Remove();
                }
            }

            // Extract formatted text content from HTML
            var textContent = ExtractFormattedTextFromHtml(doc.DocumentNode);

            // Clean up whitespace
            textContent = MultipleBlankLinesRegex.Replace(textContent, "\n\n");
            textContent = HorizontalWhitespaceRegex.Replace(textContent, " ");
            textContent = textContent.Trim();

            if (string.IsNullOrWhiteSpace(textContent))
            {
                throw new ArgumentException("HTML content contains no extractable text");
            }

            // Create PDF using QuestPDF
            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return Task.FromResult(outputStream);
        }

        /// <summary>
        /// Extracts formatted text from an HTML node, preserving basic structure.
        /// </summary>
        private string ExtractFormattedTextFromHtml(HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                return node.InnerText;
            }

            if (node.NodeType == HtmlNodeType.Comment)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var tagName = node.Name.ToLowerInvariant();

            // Add prefix for headings
            switch (tagName)
            {
                case "h1":
                    sb.AppendLine();
                    sb.AppendLine("=".PadRight(60, '='));
                    break;
                case "h2":
                    sb.AppendLine();
                    sb.AppendLine(new string('-', 40));
                    break;
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    sb.AppendLine();
                    sb.Append("### ");
                    break;
            }

            // Process children
            foreach (var child in node.ChildNodes)
            {
                var childText = ExtractFormattedTextFromHtml(child);

                // Handle inline formatting
                if (child.NodeType == HtmlNodeType.Element)
                {
                    var childTagName = child.Name.ToLowerInvariant();
                    switch (childTagName)
                    {
                        case "strong":
                        case "b":
                            childText = $"**{childText.Trim()}** ";
                            break;
                        case "em":
                        case "i":
                            childText = $"_{childText.Trim()}_ ";
                            break;
                        case "a":
                            var href = child.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                childText = $"{childText.Trim()} [{href}] ";
                            }
                            break;
                        case "li":
                            childText = $"  - {childText.Trim()}";
                            break;
                    }
                }

                sb.Append(childText);
            }

            // Add suffix/line breaks for block elements
            if (BlockElements.Contains(tagName) || tagName.StartsWith("h"))
            {
                sb.AppendLine();
            }

            // Handle lists
            if (tagName == "ul" || tagName == "ol")
            {
                sb.AppendLine();
            }

            // Handle tables - simple text representation
            if (tagName == "tr")
            {
                // End of table row - add separator
                sb.AppendLine();
            }
            else if (tagName == "td" || tagName == "th")
            {
                sb.Append(" | ");
            }

            return sb.ToString();
        }

        #endregion

        #region HEIC/HEIF Conversions

        /// <summary>
        /// Converts a HEIC/HEIF image to JPG format using SkiaSharp.
        /// HEIC is the High Efficiency Image Format used by iPhones.
        /// </summary>
        public Task<MemoryStream> ConvertHeicToJpg(MemoryStream heicStream) =>
            ConvertModernImageFormat(heicStream, SKEncodedImageFormat.Jpeg, 90, "HEIC/HEIF");

        /// <summary>
        /// Converts a HEIC/HEIF image to PNG format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertHeicToPng(MemoryStream heicStream) =>
            ConvertModernImageFormat(heicStream, SKEncodedImageFormat.Png, 0, "HEIC/HEIF");

        /// <summary>
        /// Converts a HEIC/HEIF image to WebP format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertHeicToWebp(MemoryStream heicStream) =>
            ConvertModernImageFormat(heicStream, SKEncodedImageFormat.Webp, 90, "HEIC/HEIF");

        #endregion

        #region AVIF Conversions

        /// <summary>
        /// Converts an AVIF image to JPG format using SkiaSharp.
        /// AVIF is a modern image format with superior compression.
        /// </summary>
        public Task<MemoryStream> ConvertAvifToJpg(MemoryStream avifStream) =>
            ConvertModernImageFormat(avifStream, SKEncodedImageFormat.Jpeg, 90, "AVIF");

        /// <summary>
        /// Converts an AVIF image to PNG format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertAvifToPng(MemoryStream avifStream) =>
            ConvertModernImageFormat(avifStream, SKEncodedImageFormat.Png, 0, "AVIF");

        /// <summary>
        /// Converts an AVIF image to WebP format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertAvifToWebp(MemoryStream avifStream) =>
            ConvertModernImageFormat(avifStream, SKEncodedImageFormat.Webp, 90, "AVIF");

        #endregion

        #region JPEG XL Conversions

        /// <summary>
        /// Converts a JPEG XL (JXL) image to JPG format using SkiaSharp.
        /// JPEG XL is a next-generation image format with excellent compression.
        /// </summary>
        public Task<MemoryStream> ConvertJxlToJpg(MemoryStream jxlStream) =>
            ConvertModernImageFormat(jxlStream, SKEncodedImageFormat.Jpeg, 90, "JPEG XL");

        /// <summary>
        /// Converts a JPEG XL (JXL) image to PNG format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertJxlToPng(MemoryStream jxlStream) =>
            ConvertModernImageFormat(jxlStream, SKEncodedImageFormat.Png, 0, "JPEG XL");

        /// <summary>
        /// Converts a JPEG XL (JXL) image to WebP format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertJxlToWebp(MemoryStream jxlStream) =>
            ConvertModernImageFormat(jxlStream, SKEncodedImageFormat.Webp, 90, "JPEG XL");

        #endregion

        #region DNG Conversions

        /// <summary>
        /// Converts a DNG (Adobe Digital Negative) raw image to JPG format using SkiaSharp.
        /// DNG is a raw image format used by various digital cameras.
        /// </summary>
        public Task<MemoryStream> ConvertDngToJpg(MemoryStream dngStream) =>
            ConvertModernImageFormat(dngStream, SKEncodedImageFormat.Jpeg, 90, "DNG");

        /// <summary>
        /// Converts a DNG (Adobe Digital Negative) raw image to PNG format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertDngToPng(MemoryStream dngStream) =>
            ConvertModernImageFormat(dngStream, SKEncodedImageFormat.Png, 0, "DNG");

        /// <summary>
        /// Converts a DNG (Adobe Digital Negative) raw image to WebP format using SkiaSharp.
        /// </summary>
        public Task<MemoryStream> ConvertDngToWebp(MemoryStream dngStream) =>
            ConvertModernImageFormat(dngStream, SKEncodedImageFormat.Webp, 90, "DNG");

        #endregion

        #region Helper Methods

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
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

        /// <summary>
        /// Converts a modern image format (HEIC, AVIF, JXL, DNG) to a standard format using SkiaSharp.
        /// Centralizes conversion logic to avoid code duplication and ensure consistent error handling.
        ///
        /// SkiaSharp 3.116.1+ supports these formats natively:
        /// - HEIF/HEIC: Added in v1.68.1 (SKEncodedImageFormat.Heif)
        /// - AVIF: Added in v2.88.1 (SKEncodedImageFormat.Avif)
        /// - DNG: Added in v1.53.0 (SKEncodedImageFormat.Dng)
        /// - JPEG XL: Added in v3.0.0 (SKEncodedImageFormat.Jpegxl)
        /// </summary>
        /// <param name="inputStream">The input image stream</param>
        /// <param name="targetFormat">The target encoded image format (Jpeg, Png, Webp)</param>
        /// <param name="quality">Encoding quality (0-100). For PNG, this is ignored so use 0.</param>
        /// <param name="formatName">Human-readable format name for error messages</param>
        /// <returns>A memory stream containing the converted image</returns>
        private Task<MemoryStream> ConvertModernImageFormat(MemoryStream inputStream, SKEncodedImageFormat targetFormat, int quality, string formatName)
        {
            // Validate input size to prevent memory exhaustion
            if (inputStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input image exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            inputStream.Position = 0;

            SKBitmap bitmap;
            try
            {
                // Use stream directly to avoid unnecessary memory allocation from ToArray()
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

        #region PDF to Image Conversion Methods

        /// <summary>
        /// Converts a PDF document to PNG image format.
        /// Extracts text content from the PDF and renders it to a PNG image using SkiaSharp.
        /// For multi-page PDFs, renders the first page.
        /// </summary>
        /// <param name="pdfStream">The PDF stream to convert</param>
        /// <returns>A PNG image stream containing the rendered PDF content</returns>
        public async Task<MemoryStream> ConvertPdfToPng(MemoryStream pdfStream)
        {
            return await ConvertPdfToImage(pdfStream, SKEncodedImageFormat.Png, SKColors.White, 100);
        }

        /// <summary>
        /// Converts a PDF document to JPG/JPEG image format.
        /// Extracts text content from the PDF and renders it to a JPG image using SkiaSharp.
        /// For multi-page PDFs, renders the first page.
        /// </summary>
        /// <param name="pdfStream">The PDF stream to convert</param>
        /// <returns>A JPG image stream containing the rendered PDF content</returns>
        public async Task<MemoryStream> ConvertPdfToJpg(MemoryStream pdfStream)
        {
            return await ConvertPdfToImage(pdfStream, SKEncodedImageFormat.Jpeg, SKColors.White, 80);
        }

        /// <summary>
        /// Core method for PDF to image conversion.
        /// Uses PdfPig to extract text content and SkiaSharp to render to an image.
        /// </summary>
        private async Task<MemoryStream> ConvertPdfToImage(
            MemoryStream pdfStream,
            SKEncodedImageFormat format,
            SKColor backgroundColor,
            int quality)
        {
            // Security: Validate input size before processing
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
            List<UglyToad.PdfPig.Content.Page> pages;
            try
            {
                pages = pdfDoc.GetPages().ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read PDF pages: {ex.Message}", ex);
            }

            if (pages.Count == 0)
            {
                throw new ArgumentException("PDF document contains no pages");
            }

            // Render first page
            var firstPage = pages[0];
            var pageText = firstPage.Text;

            if (string.IsNullOrWhiteSpace(pageText))
            {
                throw new ArgumentException("PDF page contains no extractable text content");
            }

            // Define image dimensions (A4 proportions at 150 DPI equivalent)
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

                // Split text into lines and wrap long lines
                var textLines = pageText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var wrappedLines = new List<string>();

                foreach (var line in textLines)
                {
                    // Security: Limit number of lines processed to prevent unbounded memory allocation
                    if (wrappedLines.Count >= MaxTextLinesForImageConversion)
                        break;

                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    // Wrap long lines
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

                // Draw text lines
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
            return await Task.FromResult(outputStream);
        }

        #endregion

        #region PPTX to Image Conversion Methods

        /// <summary>
        /// Converts a PowerPoint presentation to PNG image format.
        /// Extracts text content from the first slide and renders it to a PNG image.
        /// </summary>
        /// <param name="pptxStream">The PPTX stream to convert</param>
        /// <returns>A PNG image stream containing the rendered slide content</returns>
        public async Task<MemoryStream> ConvertPptxToPng(MemoryStream pptxStream)
        {
            return await ConvertPptxToImage(pptxStream, SKEncodedImageFormat.Png, SKColors.White, 100);
        }

        /// <summary>
        /// Converts a PowerPoint presentation to JPG/JPEG image format.
        /// Extracts text content from the first slide and renders it to a JPG image.
        /// </summary>
        /// <param name="pptxStream">The PPTX stream to convert</param>
        /// <returns>A JPG image stream containing the rendered slide content</returns>
        public async Task<MemoryStream> ConvertPptxToJpg(MemoryStream pptxStream)
        {
            return await ConvertPptxToImage(pptxStream, SKEncodedImageFormat.Jpeg, SKColors.White, 80);
        }

        /// <summary>
        /// Core method for PPTX to image conversion.
        /// Uses DocumentFormat.OpenXml to extract text content and SkiaSharp to render to an image.
        /// </summary>
        private async Task<MemoryStream> ConvertPptxToImage(
            MemoryStream pptxStream,
            SKEncodedImageFormat format,
            SKColor backgroundColor,
            int quality)
        {
            // Security: Validate input size before processing
            if (pptxStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            pptxStream.Position = 0;

            var slideTexts = new List<string>();
            var pptxCopy = new MemoryStream(pptxStream.ToArray(), true);

            DocumentFormat.OpenXml.Packaging.PresentationDocument presentation;
            try
            {
                presentation = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(pptxCopy, false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse PPTX document: {ex.Message}", ex);
            }

            using var presDoc = presentation;
            var presentationPart = presDoc.PresentationPart;

            if (presentationPart == null)
            {
                throw new ArgumentException("PPTX file has no presentation part");
            }

            IEnumerable<DocumentFormat.OpenXml.Packaging.SlidePart> slideParts;
            try
            {
                slideParts = presentationPart.SlideParts;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read PPTX slide parts: {ex.Message}", ex);
            }

            if (slideParts == null || !slideParts.Any())
            {
                throw new ArgumentException("PPTX presentation contains no slides");
            }

            // Get text from the first slide
            var firstSlide = slideParts.First();
            if (firstSlide?.Slide?.CommonSlideData?.ShapeTree != null)
            {
                var slideText = ExtractTextFromSlide(firstSlide.Slide.CommonSlideData.ShapeTree);
                if (!string.IsNullOrWhiteSpace(slideText))
                {
                    slideTexts.Add(slideText);
                }
            }

            if (slideTexts.Count == 0)
            {
                throw new ArgumentException("PPTX slide contains no extractable text content");
            }

            var textContent = slideTexts[0];

            // Define image dimensions (16:9 presentation proportions)
            const int width = 1920;
            const int height = 1080;
            const int margin = 80;
            const int titleHeight = 60;
            const int lineHeight = 32;
            const int maxCharsPerLine = 100;

            var outputStream = new MemoryStream();

            using (var bitmap = new SKBitmap(width, height))
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(backgroundColor);

                // Draw slide title
                using var titlePaint = new SKPaint
                {
                    Color = SKColors.DarkBlue,
                    IsAntialias = true
                };
                using var titleFont = new SKFont(
                    SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                    36);

                canvas.DrawText("Slide 1", margin, margin + titleHeight / 2f, titleFont, titlePaint);

                // Draw separator line
                using var linePaint = new SKPaint
                {
                    Color = SKColors.LightGray,
                    StrokeWidth = 2,
                    Style = SKPaintStyle.Stroke
                };
                canvas.DrawLine(margin, margin + titleHeight, width - margin, margin + titleHeight, linePaint);

                // Draw content text
                using var contentPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    IsAntialias = true
                };
                using var contentFont = new SKFont(SKTypeface.FromFamilyName("Arial"), 20);

                // Split text into lines and wrap long lines
                var textLines = textContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var wrappedLines = new List<string>();

                foreach (var line in textLines)
                {
                    // Security: Limit number of lines processed to prevent unbounded memory allocation
                    if (wrappedLines.Count >= MaxTextLinesForImageConversion)
                        break;

                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;

                    // Wrap long lines
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

                // Draw text lines below title
                float y = margin + titleHeight + 40;
                int maxLines = (height - (int)y - margin) / lineHeight;

                foreach (var textLine in wrappedLines.Take(maxLines))
                {
                    canvas.DrawText(textLine, margin, y, contentFont, contentPaint);
                    y += lineHeight;
                }

                canvas.Flush();

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(format, quality);
                data.SaveTo(outputStream);
            }

            outputStream.Position = 0;
            return await Task.FromResult(outputStream);
        }

        #endregion
    }
}
