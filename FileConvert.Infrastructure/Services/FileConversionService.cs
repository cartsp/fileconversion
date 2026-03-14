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
        private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
        {
            "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6", "li", "tr"
        };
        private static IImmutableList<ConvertorDetails> Convertors = ImmutableList<ConvertorDetails>.Empty;

        static FileConversionService()
        {
            // EPPlus 5+ requires license context to be set
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            // QuestPDF requires license - Community Edition is free for non-commercial use
            QuestPDF.Settings.License = LicenseType.Community;
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
            //ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.docx, FileExtension.html, ConvertDocToHTML));
            //ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.docx, FileExtension.pdf, ConvertDocToPDF));
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
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.png, ConvertTiffToPng));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.jpg, ConvertTiffToJpg));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.jpeg, ConvertTiffToJpg));

            // TSV → JSON conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.tsv, FileExtension.json, ConvertTSVToJSON));

            // XML → CSV conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.csv, ConvertXMLToCSV));

            // CSV → YAML conversion
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.yaml, ConvertCSVToYAML));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.yml, ConvertCSVToYAML));

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

            // Image to PDF conversions - very high value for users
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.pdf, ConvertImageToPdf));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.pdf, ConvertImageToPdf));

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
                StreamUtils.Copy(gzipStream, outputStream, buffer);
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
                StreamUtils.Copy(bzip2Stream, outputStream, buffer);
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

            Document.Create(container =>
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

        #endregion
    }
}
