using FileConvert.Core;
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
using FileConvert.Core.ValueObjects;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Markdig;

namespace FileConvert.Infrastructure
{
    public class FileConversionService : IFileConvertors
    {
        private static readonly MarkdownPipeline CachedMarkdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        private static IImmutableList<ConvertorDetails> Convertors = ImmutableList<ConvertorDetails>.Empty;

        static FileConversionService()
        {
            // EPPlus 5+ requires license context to be set
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
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

            using (Image image = Image.Load(PNGStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, new JpegEncoder() { Quality = 80 });
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertImageToPNG(MemoryStream ImageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image image = Image.Load(ImageStream.ToArray()))
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

            using (Image image = Image.Load(ImageStream.ToArray()))
            {
                image.SaveAsGif(outputStream);
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
                        // Escape quotes and wrap in quotes if contains comma or quote
                        if (cellValue.Contains(',') || cellValue.Contains('"'))
                        {
                            cellValue = "\"" + cellValue.Replace("\"", "\"\"") + "\"";
                        }
                        rowValues.Add(cellValue);
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

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(JsonSerializer.Serialize(jsonResult, options));
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
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
    }
}
