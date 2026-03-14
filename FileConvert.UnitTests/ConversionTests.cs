using FileConvert.Core.ValueObjects;
using FileConvert.Infrastructure;
using OfficeOpenXml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Tiff;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YamlDotNet.Core;

namespace FileConvert.UnitTests
{
    public class ConversionTests
    {
        static ConversionTests()
        {
            // EPPlus 5+ requires license context to be set
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public static FileConversionService conversionService = new FileConversionService();
        [Theory]
        [InlineData(".xlsx")]
        public void TestAvailableConversionsForCSV(string conversionAvailable)
        {
            //Arrange
            MemoryStream officeDocStream = new MemoryStream();
            var DocumentName = "testdoc.csv";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        [Fact]
        public void TestGetAllAvailableConversions()
        {
            //Arrange
            MemoryStream officeDocStream = new MemoryStream();

            //Act
            var result = conversionService.GetAllAvailableConvertors();

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Count != 0);
            Assert.Equal(123, result.Count);
        }

        [Fact]
        public async Task TestConvertingWordDocToHTMLReturnsStream()
        {
            //Arrange
            var officeDocStream = ConvertFileToMemoryStream("Documents/Test Document.docx");

            //Act
            var result = await conversionService.ConvertDocToHTML(officeDocStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData("Documents/Untitled 1.csv", "hi")]
        [InlineData("Documents/addresses.csv", "John")]
        [InlineData("Documents/cities.csv", "\"LatD\"")]
        public async Task TestConvertingCSVToXLXS(string documentToTest, string expectedValue)
        {
            //Arrange
            var officeDocStream = ConvertFileToMemoryStream(documentToTest);

            //Act
            var result = await conversionService.ConvertCSVToExcel(officeDocStream);
            string foundValueInA1;

            using (ExcelPackage package = new ExcelPackage(result))
            {
                foundValueInA1 = package.Workbook.Worksheets[0].Cells[1, 1].Value.ToString();
            }

            //Assert
            Assert.Equal(expectedValue, foundValueInA1);
        }

        #region Image tests

        [Fact]
        public async Task TestConvertingPNGToJPG()
        {
            //Arrange
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.png)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(pngStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingPNGToGIF()
        {
            //Arrange
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.png)
                                        .ThatConvertTo(FileExtension.gif)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(pngStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, GifFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingGIFToJPG()
        {
            //Arrange
            MemoryStream gifStream = ConvertFileToMemoryStream("Documents/sample.gif");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.gif)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(gifStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingGIFToPNG()
        {
            //Arrange
            MemoryStream imageStream = ConvertFileToMemoryStream("Documents/sample.gif");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.gif)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(imageStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJPGToPNG()
        {
            //Arrange
            MemoryStream imageStream = ConvertFileToMemoryStream("Documents/example.jpg");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.jpg)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(imageStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJPGToGIF()
        {
            //Arrange
            MemoryStream imageStream = ConvertFileToMemoryStream("Documents/example.jpg");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.jpg)
                                        .ThatConvertTo(FileExtension.gif)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(imageStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, GifFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingBMPToGIF()
        {
            //Arrange
            MemoryStream imageStream = ConvertFileToMemoryStream("Documents/example.bmp");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.bmp)
                                        .ThatConvertTo(FileExtension.gif)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(imageStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, GifFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingBMPToJPG()
        {
            //Arrange
            MemoryStream imageStream = ConvertFileToMemoryStream("Documents/example.bmp");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.bmp)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(imageStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingBMPToPNG()
        {
            //Arrange
            MemoryStream imageStream = ConvertFileToMemoryStream("Documents/example.bmp");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.bmp)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(imageStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        #endregion Image tests

        #region JSON to XML tests

        [Fact]
        public async Task TestConvertingJSONToXML()
        {
            //Arrange
            MemoryStream jsonStream = ConvertFileToMemoryStream("Documents/test.json");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.json)
                                        .ThatConvertTo(FileExtension.xml)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jsonStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var xmlContent = await reader.ReadToEndAsync();
            Assert.Contains("<Root>", xmlContent);
            Assert.Contains("<name>Test</name>", xmlContent);
            Assert.Contains("<value>123</value>", xmlContent);
            Assert.Contains("<items>", xmlContent);
        }

        [Fact]
        public async Task TestConvertingJSONToXMLReturnsStream()
        {
            //Arrange
            MemoryStream jsonStream = ConvertFileToMemoryStream("Documents/test.json");

            //Act
            var result = await conversionService.ConvertJSONToXML(jsonStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".xml")]
        public void TestAvailableConversionsForJSON(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.json";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion JSON to XML tests

        #region Markdown to HTML tests

        [Fact]
        public async Task TestConvertingMarkdownToHTML()
        {
            //Arrange
            MemoryStream mdStream = ConvertFileToMemoryStream("Documents/test.md");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.md)
                                        .ThatConvertTo(FileExtension.html)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(mdStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var htmlContent = await reader.ReadToEndAsync();
            Assert.Contains("Heading 1</h1>", htmlContent);
            Assert.Contains("<strong>bold</strong>", htmlContent);
            Assert.Contains("<em>italic</em>", htmlContent);
            Assert.Contains("Heading 2</h2>", htmlContent);
            Assert.Contains("<li>List item 1</li>", htmlContent);
            Assert.Contains("<li>List item 2</li>", htmlContent);
            Assert.Contains("<a href=\"https://example.com\">Link</a>", htmlContent);
        }

        [Fact]
        public async Task TestConvertingMarkdownToHTMLReturnsStream()
        {
            //Arrange
            MemoryStream mdStream = ConvertFileToMemoryStream("Documents/test.md");

            //Act
            var result = await conversionService.ConvertMarkdownToHTML(mdStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".html")]
        [InlineData(".pdf")]
        public void TestAvailableConversionsForMarkdown(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.md";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion Markdown to HTML tests

        #region XML to JSON tests

        [Fact]
        public async Task TestConvertingXMLToJSON()
        {
            //Arrange
            MemoryStream xmlStream = ConvertFileToMemoryStream("Documents/test.xml");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.xml)
                                        .ThatConvertTo(FileExtension.json)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(xmlStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var jsonContent = await reader.ReadToEndAsync();
            Assert.Contains("Test", jsonContent);
            Assert.Contains("123", jsonContent);
        }

        [Fact]
        public async Task TestConvertingXMLToJSONReturnsStream()
        {
            //Arrange
            MemoryStream xmlStream = ConvertFileToMemoryStream("Documents/test.xml");

            //Act
            var result = await conversionService.ConvertXMLToJSON(xmlStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".json")]
        public void TestAvailableConversionsForXML(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.xml";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion XML to JSON tests

        #region XLSX to CSV tests

        [Fact]
        public async Task TestConvertingXLSXToCSV()
        {
            //Arrange
            MemoryStream xlsxStream = ConvertFileToMemoryStream("Documents/test.xlsx");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.xlsx)
                                        .ThatConvertTo(FileExtension.csv)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(xlsxStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var csvContent = await reader.ReadToEndAsync();
            Assert.Contains("Name", csvContent);
            Assert.Contains("Age", csvContent);
            Assert.Contains("City", csvContent);
            Assert.Contains("Alice", csvContent);
            Assert.Contains("Bob", csvContent);
        }

        [Fact]
        public async Task TestConvertingXLSXToCSVReturnsStream()
        {
            //Arrange
            MemoryStream xlsxStream = ConvertFileToMemoryStream("Documents/test.xlsx");

            //Act
            var result = await conversionService.ConvertXLSXToCSV(xlsxStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".csv")]
        public void TestAvailableConversionsForXLSX(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.xlsx";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 3);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion XLSX to CSV tests

        #region YAML to JSON tests

        [Fact]
        public async Task TestConvertingYAMLToJSON()
        {
            //Arrange
            MemoryStream yamlStream = ConvertFileToMemoryStream("Documents/test.yaml");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.yaml)
                                        .ThatConvertTo(FileExtension.json)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(yamlStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var jsonContent = await reader.ReadToEndAsync();
            Assert.Contains("Test Configuration", jsonContent);
            Assert.Contains("localhost", jsonContent);
            Assert.Contains("5432", jsonContent);
        }

        [Fact]
        public async Task TestConvertingYMLToJSON()
        {
            //Arrange
            MemoryStream yamlStream = ConvertFileToMemoryStream("Documents/test.yaml");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.yml)
                                        .ThatConvertTo(FileExtension.json)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(yamlStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var jsonContent = await reader.ReadToEndAsync();
            Assert.Contains("Test Configuration", jsonContent);
        }

        [Fact]
        public async Task TestConvertingYAMLToJSONReturnsStream()
        {
            //Arrange
            MemoryStream yamlStream = ConvertFileToMemoryStream("Documents/test.yaml");

            //Act
            var result = await conversionService.ConvertYAMLToJSON(yamlStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".json")]
        public void TestAvailableConversionsForYAML(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.yaml";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion YAML to JSON tests

        #region JSON to YAML tests

        [Fact]
        public async Task TestConvertingJSONToYAML()
        {
            //Arrange
            MemoryStream jsonStream = ConvertFileToMemoryStream("Documents/test-for-yaml.json");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.json)
                                        .ThatConvertTo(FileExtension.yaml)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jsonStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var yamlContent = await reader.ReadToEndAsync();
            Assert.Contains("Test Configuration", yamlContent);
            Assert.Contains("localhost", yamlContent);
        }

        [Fact]
        public async Task TestConvertingJSONToYAMLReturnsStream()
        {
            //Arrange
            MemoryStream jsonStream = ConvertFileToMemoryStream("Documents/test-for-yaml.json");

            //Act
            var result = await conversionService.ConvertJSONToYAML(jsonStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".yaml")]
        [InlineData(".yml")]
        public void TestAvailableConversionsForJSONToYAML(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.json";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion JSON to YAML tests

        #region XLSX to JSON tests

        [Fact]
        public async Task TestConvertingXLSXToJSON()
        {
            //Arrange
            MemoryStream xlsxStream = ConvertFileToMemoryStream("Documents/test.xlsx");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.xlsx)
                                        .ThatConvertTo(FileExtension.json)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(xlsxStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var jsonContent = await reader.ReadToEndAsync();
            Assert.Contains("Name", jsonContent);
            Assert.Contains("Age", jsonContent);
            Assert.Contains("City", jsonContent);
            Assert.Contains("Alice", jsonContent);
            Assert.Contains("Bob", jsonContent);
        }

        [Fact]
        public async Task TestConvertingXLSXToJSONReturnsStream()
        {
            //Arrange
            MemoryStream xlsxStream = ConvertFileToMemoryStream("Documents/test.xlsx");

            //Act
            var result = await conversionService.ConvertXLSXToJSON(xlsxStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".json")]
        public void TestAvailableConversionsForXLSXToJSON(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.xlsx";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion XLSX to JSON tests

        #region TSV to CSV tests

        [Fact]
        public async Task TestConvertingTSVToCSV()
        {
            //Arrange
            MemoryStream tsvStream = ConvertFileToMemoryStream("Documents/test.tsv");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tsv)
                                        .ThatConvertTo(FileExtension.csv)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tsvStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var csvContent = await reader.ReadToEndAsync();
            Assert.Contains("Name,Age,City", csvContent);
            Assert.Contains("Alice,30,New York", csvContent);
            Assert.Contains("Bob,25,Los Angeles", csvContent);
        }

        [Fact]
        public async Task TestConvertingTSVToCSVReturnsStream()
        {
            //Arrange
            MemoryStream tsvStream = ConvertFileToMemoryStream("Documents/test.tsv");

            //Act
            var result = await conversionService.ConvertTSVToCSV(tsvStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".csv")]
        public void TestAvailableConversionsForTSV(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.tsv";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion TSV to CSV tests

        #region CSV to JSON tests

        [Fact]
        public async Task TestConvertingCSVToJSON()
        {
            //Arrange
            MemoryStream csvStream = ConvertFileToMemoryStream("Documents/test-data.csv");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.csv)
                                        .ThatConvertTo(FileExtension.json)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(csvStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var jsonContent = await reader.ReadToEndAsync();
            Assert.Contains("Name", jsonContent);
            Assert.Contains("Age", jsonContent);
            Assert.Contains("City", jsonContent);
            Assert.Contains("Alice", jsonContent);
            Assert.Contains("Bob", jsonContent);
        }

        [Fact]
        public async Task TestConvertingCSVToJSONReturnsStream()
        {
            //Arrange
            MemoryStream csvStream = ConvertFileToMemoryStream("Documents/test-data.csv");

            //Act
            var result = await conversionService.ConvertCSVToJSON(csvStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".json")]
        public void TestAvailableConversionsForCSVToJSON(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.csv";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion CSV to JSON tests

        #region JSON to CSV tests

        [Fact]
        public async Task TestConvertingJSONToCSV()
        {
            //Arrange
            MemoryStream jsonStream = ConvertFileToMemoryStream("Documents/test-array.json");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.json)
                                        .ThatConvertTo(FileExtension.csv)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jsonStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var csvContent = await reader.ReadToEndAsync();
            Assert.Contains("Name", csvContent);
            Assert.Contains("Age", csvContent);
            Assert.Contains("Alice", csvContent);
            Assert.Contains("Bob", csvContent);
        }

        [Fact]
        public async Task TestConvertingJSONToCSVReturnsStream()
        {
            //Arrange
            MemoryStream jsonStream = ConvertFileToMemoryStream("Documents/test-array.json");

            //Act
            var result = await conversionService.ConvertJSONToCSV(jsonStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".csv")]
        public void TestAvailableConversionsForJSONToCSV(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.json";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion JSON to CSV tests

        #region HTML to Text tests

        [Fact]
        public async Task TestConvertingHTMLToText()
        {
            //Arrange
            MemoryStream htmlStream = ConvertFileToMemoryStream("Documents/test.html");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.html)
                                        .ThatConvertTo(FileExtension.txt)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(htmlStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var textContent = await reader.ReadToEndAsync();
            Assert.Contains("Hello World", textContent);
            Assert.DoesNotContain("<html>", textContent);
            Assert.DoesNotContain("<body>", textContent);
        }

        [Fact]
        public async Task TestConvertingHTMLToTextReturnsStream()
        {
            //Arrange
            MemoryStream htmlStream = ConvertFileToMemoryStream("Documents/test.html");

            //Act
            var result = await conversionService.ConvertHTMLToText(htmlStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".txt")]
        public void TestAvailableConversionsForHTML(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.html";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 1);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion HTML to Text tests

        #region WebP image tests

        [Fact]
        public async Task TestConvertingPNGToWebP()
        {
            //Arrange
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.png)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(pngStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJPGToWebP()
        {
            //Arrange
            MemoryStream jpgStream = ConvertFileToMemoryStream("Documents/example.jpg");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.jpg)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jpgStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingGIFToWebP()
        {
            //Arrange
            MemoryStream gifStream = ConvertFileToMemoryStream("Documents/sample.gif");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.gif)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(gifStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingBMPToWebP()
        {
            //Arrange
            MemoryStream bmpStream = ConvertFileToMemoryStream("Documents/example.bmp");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.bmp)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(bmpStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingWebPToJPG()
        {
            //Arrange
            MemoryStream webpStream = ConvertFileToMemoryStream("Documents/test.webp");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.webp)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(webpStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingWebPToPNG()
        {
            //Arrange
            MemoryStream webpStream = ConvertFileToMemoryStream("Documents/test.webp");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.webp)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(webpStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingWebPToGIF()
        {
            //Arrange
            MemoryStream webpStream = ConvertFileToMemoryStream("Documents/test.webp");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.webp)
                                        .ThatConvertTo(FileExtension.gif)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(webpStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, GifFormat.Instance));
        }

        [Theory]
        [InlineData(".jpg")]
        [InlineData(".png")]
        [InlineData(".gif")]
        [InlineData(".ico")]
        [InlineData(".jpeg")]
        [InlineData(".pdf")]
        public void TestAvailableConversionsForWebP(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.webp";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 6);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion WebP image tests

        #region TIFF image tests

        [Fact]
        public async Task TestConvertingTIFToPNG()
        {
            //Arrange
            MemoryStream tifStream = ConvertFileToMemoryStream("Documents/test.tif");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tif)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tifStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingTIFToJPG()
        {
            //Arrange
            MemoryStream tifStream = ConvertFileToMemoryStream("Documents/test.tif");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tif)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tifStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingTIFFToPNG()
        {
            //Arrange
            MemoryStream tiffStream = ConvertFileToMemoryStream("Documents/test.tif");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tiff)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tiffStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingTIFFToJPG()
        {
            //Arrange
            MemoryStream tiffStream = ConvertFileToMemoryStream("Documents/test.tif");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tiff)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tiffStream);

            //Assert
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Theory]
        [InlineData(".jpg")]
        [InlineData(".png")]
        [InlineData(".jpeg")]
        [InlineData(".webp")]
        public void TestAvailableConversionsForTIF(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.tif";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion TIFF image tests

        #region TSV to JSON tests

        [Fact]
        public async Task TestConvertingTSVToJSON()
        {
            //Arrange
            MemoryStream tsvStream = ConvertFileToMemoryStream("Documents/test.tsv");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tsv)
                                        .ThatConvertTo(FileExtension.json)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tsvStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var jsonContent = await reader.ReadToEndAsync();
            Assert.Contains("Name", jsonContent);
            Assert.Contains("Age", jsonContent);
            Assert.Contains("City", jsonContent);
            Assert.Contains("Alice", jsonContent);
            Assert.Contains("Bob", jsonContent);
        }

        [Fact]
        public async Task TestConvertingTSVToJSONReturnsStream()
        {
            //Arrange
            MemoryStream tsvStream = ConvertFileToMemoryStream("Documents/test.tsv");

            //Act
            var result = await conversionService.ConvertTSVToJSON(tsvStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".json")]
        public void TestAvailableConversionsForTSVToJSON(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.tsv";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion TSV to JSON tests

        #region XML to CSV tests

        [Fact]
        public async Task TestConvertingXMLToCSV()
        {
            //Arrange
            MemoryStream xmlStream = ConvertFileToMemoryStream("Documents/test-for-xml-to-csv.xml");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.xml)
                                        .ThatConvertTo(FileExtension.csv)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(xmlStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var csvContent = await reader.ReadToEndAsync();
            Assert.Contains("name", csvContent);
            Assert.Contains("age", csvContent);
        }

        [Fact]
        public async Task TestConvertingXMLToCSVReturnsStream()
        {
            //Arrange
            MemoryStream xmlStream = ConvertFileToMemoryStream("Documents/test-for-xml-to-csv.xml");

            //Act
            var result = await conversionService.ConvertXMLToCSV(xmlStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".csv")]
        public void TestAvailableConversionsForXMLToCSV(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.xml";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion XML to CSV tests

        #region CSV to YAML tests

        [Fact]
        public async Task TestConvertingCSVToYAML()
        {
            //Arrange
            MemoryStream csvStream = ConvertFileToMemoryStream("Documents/test-data.csv");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.csv)
                                        .ThatConvertTo(FileExtension.yaml)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(csvStream);

            //Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var yamlContent = await reader.ReadToEndAsync();
            Assert.Contains("Name", yamlContent);
            Assert.Contains("Age", yamlContent);
            Assert.Contains("City", yamlContent);
        }

        [Fact]
        public async Task TestConvertingCSVToYAMLReturnsStream()
        {
            //Arrange
            MemoryStream csvStream = ConvertFileToMemoryStream("Documents/test-data.csv");

            //Act
            var result = await conversionService.ConvertCSVToYAML(csvStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".yaml")]
        [InlineData(".yml")]
        public void TestAvailableConversionsForCSVToYAML(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.csv";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion CSV to YAML tests

        #region ICO conversion tests

        [Fact]
        public async Task TestConvertingPNGToICO()
        {
            //Arrange
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.png)
                                        .ThatConvertTo(FileExtension.ico)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(pngStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify ICO header
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var reserved = reader.ReadUInt16();
            var type = reader.ReadUInt16();
            var count = reader.ReadUInt16();
            Assert.Equal(0, reserved);
            Assert.Equal(1, type);  // 1 = ICO type
            Assert.Equal(1, count); // Should have 1 image
        }

        [Fact]
        public async Task TestConvertingJPGToICO()
        {
            //Arrange
            MemoryStream jpgStream = ConvertFileToMemoryStream("Documents/example.jpg");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.jpg)
                                        .ThatConvertTo(FileExtension.ico)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jpgStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify ICO header
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var reserved = reader.ReadUInt16();
            var type = reader.ReadUInt16();
            Assert.Equal(0, reserved);
            Assert.Equal(1, type);  // 1 = ICO type
        }

        [Fact]
        public async Task TestConvertingICOToPNG()
        {
            //Arrange - First create an ICO file from PNG
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");
            var icoStream = await conversionService.ConvertImageToIco(pngStream);
            icoStream.Position = 0;

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.ico)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(icoStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingPNGToICOReturnsStream()
        {
            //Arrange
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");

            //Act
            var result = await conversionService.ConvertImageToIco(pngStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingICOToPNGReturnsStream()
        {
            //Arrange - First create an ICO file from PNG
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");
            var icoStream = await conversionService.ConvertImageToIco(pngStream);
            icoStream.Position = 0;

            //Act
            var result = await conversionService.ConvertIcoToPng(icoStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".png")]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        [InlineData(".gif")]
        [InlineData(".webp")]
        [InlineData(".bmp")]
        public void TestAvailableConversionsToICO(string sourceExtension)
        {
            //Arrange
            var DocumentName = $"testdoc{sourceExtension}";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.Contains(result, a => a.ConvertedExtension.Value == ".ico");
        }

        [Theory]
        [InlineData(".png")]
        public void TestAvailableConversionsFromICO(string targetExtension)
        {
            //Arrange
            var DocumentName = "testdoc.ico";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == targetExtension);
        }

        #endregion ICO conversion tests

        #region SVG conversion tests

        [Fact]
        public async Task TestConvertingSVGToPNG()
        {
            //Arrange
            MemoryStream svgStream = ConvertFileToMemoryStream("Documents/test.svg");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.svg)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(svgStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingSVGToJPG()
        {
            //Arrange
            MemoryStream svgStream = ConvertFileToMemoryStream("Documents/test.svg");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.svg)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(svgStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingSVGToWebP()
        {
            //Arrange
            MemoryStream svgStream = ConvertFileToMemoryStream("Documents/test.svg");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.svg)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(svgStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingSVGToPNGReturnsStream()
        {
            //Arrange
            MemoryStream svgStream = ConvertFileToMemoryStream("Documents/test.svg");

            //Act
            var result = await conversionService.ConvertSvgToPng(svgStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingSVGToJPGReturnsStream()
        {
            //Arrange
            MemoryStream svgStream = ConvertFileToMemoryStream("Documents/test.svg");

            //Act
            var result = await conversionService.ConvertSvgToJpg(svgStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingSVGToWebPReturnsStream()
        {
            //Arrange
            MemoryStream svgStream = ConvertFileToMemoryStream("Documents/test.svg");

            //Act
            var result = await conversionService.ConvertSvgToWebP(svgStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".png")]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        [InlineData(".webp")]
        public void TestAvailableConversionsForSVG(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.svg";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion SVG conversion tests

        #region Archive conversion tests

        [Fact]
        public async Task TestConvertingGZToTar()
        {
            //Arrange
            MemoryStream gzStream = ConvertFileToMemoryStream("Documents/test.tar.gz");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.gz)
                                        .ThatConvertTo(FileExtension.tar)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(gzStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify TAR header - TAR files start with filename
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var headerBytes = reader.ReadBytes(8);
            // TAR files have the filename at the start, null-padded to 100 bytes
            Assert.True(headerBytes.Length > 0);
        }

        [Fact]
        public async Task TestConvertingTarToGz()
        {
            //Arrange
            MemoryStream tarStream = ConvertFileToMemoryStream("Documents/test.tar");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tar)
                                        .ThatConvertTo(FileExtension.gz)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tarStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify GZIP header - GZIP files start with magic bytes 1F 8B
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var magic1 = reader.ReadByte();
            var magic2 = reader.ReadByte();
            Assert.Equal(0x1F, magic1);
            Assert.Equal(0x8B, magic2);
        }

        [Fact]
        public async Task TestConvertingBZ2ToTar()
        {
            //Arrange
            MemoryStream bz2Stream = ConvertFileToMemoryStream("Documents/test.tbz2");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.bz2)
                                        .ThatConvertTo(FileExtension.tar)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(bz2Stream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Theory]
        [InlineData(".tar")]
        public void TestAvailableConversionsForGZ(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.gz";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        [Theory]
        [InlineData(".gz")]
        [InlineData(".tgz")]
        public void TestAvailableConversionsForTAR(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.tar";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        [Theory]
        [InlineData(".tar")]
        public void TestAvailableConversionsForBZ2(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.bz2";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        [Fact]
        public async Task TestConvertingGZToTarReturnsStream()
        {
            //Arrange
            MemoryStream gzStream = ConvertFileToMemoryStream("Documents/test.tar.gz");

            //Act
            var result = await conversionService.ConvertGzToTar(gzStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingTarToGzReturnsStream()
        {
            //Arrange
            MemoryStream tarStream = ConvertFileToMemoryStream("Documents/test.tar");

            //Act
            var result = await conversionService.ConvertTarToGz(tarStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingBz2ToTarReturnsStream()
        {
            //Arrange
            MemoryStream bz2Stream = ConvertFileToMemoryStream("Documents/test.tbz2");

            //Act
            var result = await conversionService.ConvertBz2ToTar(bz2Stream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        #endregion Archive conversion tests

        #region Image to PDF conversion tests

        [Theory]
        [InlineData("Documents/small-png-image.png", ".png")]
        [InlineData("Documents/example.jpg", ".jpg")]
        [InlineData("Documents/sample.gif", ".gif")]
        [InlineData("Documents/test.webp", ".webp")]
        [InlineData("Documents/example.bmp", ".bmp")]
        public async Task TestConvertingImageToPDF(string filePath, string extension)
        {
            //Arrange
            MemoryStream imageStream = ConvertFileToMemoryStream(filePath);

            FileExtension sourceExtension = extension;
            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(sourceExtension)
                                        .ThatConvertTo(FileExtension.pdf)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(imageStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify PDF header - PDF files start with %PDF
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var header = reader.ReadLine();
            Assert.StartsWith("%PDF", header);
        }

        [Theory]
        [InlineData(".png")]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        [InlineData(".gif")]
        [InlineData(".bmp")]
        [InlineData(".webp")]
        public void TestAvailableConversionsToPDF(string sourceExtension)
        {
            //Arrange
            var DocumentName = $"testdoc{sourceExtension}";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.Contains(result, a => a.ConvertedExtension.Value == ".pdf");
        }

        [Fact]
        public async Task TestConvertingImageToPdfReturnsStream()
        {
            //Arrange
            MemoryStream pngStream = ConvertFileToMemoryStream("Documents/small-png-image.png");

            //Act
            var result = await conversionService.ConvertImageToPdf(pngStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        #endregion Image to PDF conversion tests

        #region ZIP ↔ TAR conversion tests

        [Fact]
        public async Task TestConvertingZipToTar()
        {
            //Arrange
            MemoryStream zipStream = ConvertFileToMemoryStream("Documents/test.zip");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.zip)
                                        .ThatConvertTo(FileExtension.tar)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(zipStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify TAR header - TAR files start with filename
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var headerBytes = reader.ReadBytes(8);
            // TAR files have the filename at the start, null-padded to 100 bytes
            Assert.True(headerBytes.Length > 0);
        }

        [Fact]
        public async Task TestConvertingTarToZip()
        {
            //Arrange
            MemoryStream tarStream = ConvertFileToMemoryStream("Documents/test.tar");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.tar)
                                        .ThatConvertTo(FileExtension.zip)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tarStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify ZIP header - ZIP files start with PK (0x50 0x4B)
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var magic1 = reader.ReadByte();
            var magic2 = reader.ReadByte();
            Assert.Equal(0x50, magic1); // 'P'
            Assert.Equal(0x4B, magic2); // 'K'
        }

        [Theory]
        [InlineData(".tar")]
        public void TestAvailableConversionsForZIP(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.zip";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        [Fact]
        public async Task TestConvertingZipToTarReturnsStream()
        {
            //Arrange
            MemoryStream zipStream = ConvertFileToMemoryStream("Documents/test.zip");

            //Act
            var result = await conversionService.ConvertZipToTar(zipStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingTarToZipReturnsStream()
        {
            //Arrange
            MemoryStream tarStream = ConvertFileToMemoryStream("Documents/test.tar");

            //Act
            var result = await conversionService.ConvertTarToZip(tarStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        #endregion ZIP ↔ TAR conversion tests

        #region TIFF to WebP conversion tests

        [Theory]
        [InlineData(".tif")]
        [InlineData(".tiff")]
        public async Task TestConvertingTiffToWebP(string extension)
        {
            //Arrange
            MemoryStream tiffStream = ConvertFileToMemoryStream("Documents/test.tif");

            FileExtension sourceExtension = extension;
            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(sourceExtension)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(tiffStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingTiffToWebPReturnsStream()
        {
            //Arrange
            MemoryStream tiffStream = ConvertFileToMemoryStream("Documents/test.tif");

            //Act
            var result = await conversionService.ConvertTiffToWebP(tiffStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Theory]
        [InlineData(".tif")]
        [InlineData(".tiff")]
        public void TestAvailableConversionsForTiffToWebP(string extension)
        {
            //Arrange
            var DocumentName = $"testdoc{extension}";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.Contains(result, a => a.ConvertedExtension.Value == ".webp");
        }

        #endregion TIFF to WebP conversion tests

        #region Text to QR Code conversion tests

        [Fact]
        public async Task TestConvertingTextToQrCodePng()
        {
            //Arrange
            var textContent = "https://example.com";
            var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.txt)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(textStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingTextToQrCodePngReturnsStream()
        {
            //Arrange
            var textContent = "Hello QR Code World!";
            var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));

            //Act
            var result = await conversionService.ConvertTextToQrCodePng(textStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingEmptyTextToQrCodeThrowsException()
        {
            //Arrange
            var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("   "));

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await conversionService.ConvertTextToQrCodePng(textStream));
        }

        [Theory]
        [InlineData(".png")]
        public void TestAvailableConversionsForTextToQrCode(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.txt";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion Text to QR Code conversion tests

        #region Text to Barcode conversion tests

        [Fact]
        public async Task TestConvertingTextToBarcodeJpg()
        {
            //Arrange
            var textContent = "CODE128TEST";
            var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.txt)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(textStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingTextToBarcodeJpegReturnsStream()
        {
            //Arrange
            var textContent = "BARCODE123";
            var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(textContent));

            //Act
            var result = await conversionService.ConvertTextToBarcodeJpg(textStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingEmptyTextToBarcodeThrowsException()
        {
            //Arrange
            var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(""));

            //Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await conversionService.ConvertTextToBarcodeJpg(textStream));
        }

        [Theory]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        public void TestAvailableConversionsForTextToBarcode(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.txt";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion Text to Barcode conversion tests

        #region PDF Merge/Split tests

        [Fact]
        public async Task TestMergingTwoPdfsReturnsSinglePdf()
        {
            // Arrange
            var pdf1Stream = ConvertFileToMemoryStream("Documents/test.pdf");
            var pdf2Stream = ConvertFileToMemoryStream("Documents/test.pdf");
            var pdfStreams = new List<MemoryStream> { pdf1Stream, pdf2Stream };

            // Act
            var result = await conversionService.MergePdfsAsync(pdfStreams);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            result.Position = 0;
            var reader = new StreamReader(result, leaveOpen: true);
            var header = reader.ReadLine();
            Assert.StartsWith("%PDF", header);
        }

        [Fact]
        public async Task TestMergingEmptyPdfListThrowsException()
        {
            // Arrange
            var pdfStreams = new List<MemoryStream>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => conversionService.MergePdfsAsync(pdfStreams));
        }

        [Fact]
        public async Task TestSplittingPdfReturnsPages()
        {
            // Arrange
            var pdfStream = ConvertFileToMemoryStream("Documents/test.pdf");

            // Act
            var result = await conversionService.SplitPdfAsync(pdfStream);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            foreach (var page in result)
            {
                Assert.True(page.Length > 0);
                page.Position = 0;
                var reader = new StreamReader(page, leaveOpen: true);
                var header = reader.ReadLine();
                Assert.StartsWith("%PDF", header);
            }
        }

        [Fact]
        public async Task TestExtractingPdfPageReturnsSinglePage()
        {
            // Arrange
            var pdfStream = ConvertFileToMemoryStream("Documents/test.pdf");

            // Act
            var result = await conversionService.ExtractPdfPageAsync(pdfStream, 1);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            result.Position = 0;
            var reader = new StreamReader(result, leaveOpen: true);
            var header = reader.ReadLine();
            Assert.StartsWith("%PDF", header);
        }

        [Fact]
        public async Task TestExtractingInvalidPageNumberThrowsException()
        {
            // Arrange
            var pdfStream = ConvertFileToMemoryStream("Documents/test.pdf");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => conversionService.ExtractPdfPageAsync(pdfStream, 100));
        }

        [Fact]
        public async Task TestConvertingPdfToTextReturnsStream()
        {
            // Arrange
            var pdfStream = ConvertFileToMemoryStream("Documents/test.pdf");
            FileExtension sourceExtension = FileExtension.pdf;
            var availableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(sourceExtension)
                                        .ThatConvertTo(FileExtension.txt)
                                        .FirstOrDefault();

            // Act
            var result = await availableConvertor.Convert(pdfStream);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            result.Position = 0;
            using var reader = new StreamReader(result);
            var text = await reader.ReadToEndAsync();
            Assert.False(string.IsNullOrEmpty(text));
        }

        #endregion PDF Merge/Split tests

        #region 7z and RAR Archive Conversion Tests

        [Fact]
        public async Task TestConverting7zToZip()
        {
            //Arrange
            MemoryStream sevenZipStream = ConvertFileToMemoryStream("Documents/test.7z");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension._7z)
                                        .ThatConvertTo(FileExtension.zip)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(sevenZipStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify ZIP header - ZIP files start with PK (0x50 0x4B)
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var magic1 = reader.ReadByte();
            var magic2 = reader.ReadByte();
            Assert.Equal(0x50, magic1); // 'P'
            Assert.Equal(0x4B, magic2); // 'K'
        }

        [Fact]
        public async Task TestConverting7zToTar()
        {
            //Arrange
            MemoryStream sevenZipStream = ConvertFileToMemoryStream("Documents/test.7z");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension._7z)
                                        .ThatConvertTo(FileExtension.tar)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(sevenZipStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify TAR header - TAR files start with filename
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var headerBytes = reader.ReadBytes(8);
            Assert.True(headerBytes.Length > 0);
        }

        [Fact(Skip = "Requires valid RAR test file created with official RAR tool")]
        public async Task TestConvertingRarToZip()
        {
            //Arrange
            MemoryStream rarStream = ConvertFileToMemoryStream("Documents/test.rar");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.rar)
                                        .ThatConvertTo(FileExtension.zip)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(rarStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify ZIP header - ZIP files start with PK (0x50 0x4B)
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var magic1 = reader.ReadByte();
            var magic2 = reader.ReadByte();
            Assert.Equal(0x50, magic1); // 'P'
            Assert.Equal(0x4B, magic2); // 'K'
        }

        [Fact(Skip = "Requires valid RAR test file created with official RAR tool")]
        public async Task TestConvertingRarToTar()
        {
            //Arrange
            MemoryStream rarStream = ConvertFileToMemoryStream("Documents/test.rar");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.rar)
                                        .ThatConvertTo(FileExtension.tar)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(rarStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            // Verify TAR header - TAR files start with filename
            result.Position = 0;
            using var reader = new BinaryReader(result, System.Text.Encoding.Default, leaveOpen: true);
            var headerBytes = reader.ReadBytes(8);
            Assert.True(headerBytes.Length > 0);
        }

        [Fact]
        public async Task TestConverting7zToZipReturnsStream()
        {
            //Arrange
            MemoryStream sevenZipStream = ConvertFileToMemoryStream("Documents/test.7z");

            //Act
            var result = await conversionService.Convert7zToZip(sevenZipStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConverting7zToTarReturnsStream()
        {
            //Arrange
            MemoryStream sevenZipStream = ConvertFileToMemoryStream("Documents/test.7z");

            //Act
            var result = await conversionService.Convert7zToTar(sevenZipStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact(Skip = "Requires valid RAR test file created with official RAR tool")]
        public async Task TestConvertingRarToZipReturnsStream()
        {
            //Arrange
            MemoryStream rarStream = ConvertFileToMemoryStream("Documents/test.rar");

            //Act
            var result = await conversionService.ConvertRarToZip(rarStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact(Skip = "Requires valid RAR test file created with official RAR tool")]
        public async Task TestConvertingRarToTarReturnsStream()
        {
            //Arrange
            MemoryStream rarStream = ConvertFileToMemoryStream("Documents/test.rar");

            //Act
            var result = await conversionService.ConvertRarToTar(rarStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".zip")]
        [InlineData(".tar")]
        public void TestAvailableConversionsFor7z(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.7z";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        [Theory]
        [InlineData(".zip")]
        [InlineData(".tar")]
        public void TestAvailableConversionsForRAR(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.rar";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion 7z and RAR Archive Conversion Tests

        #region JPEG 2000 (JP2/J2K) Conversion Tests

        [Fact]
        public async Task TestConvertingJp2ToPng()
        {
            //Arrange
            MemoryStream jp2Stream = ConvertFileToMemoryStream("Documents/test.jp2");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.jp2)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jp2Stream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJp2ToJpg()
        {
            //Arrange
            MemoryStream jp2Stream = ConvertFileToMemoryStream("Documents/test.jp2");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.jp2)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jp2Stream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJp2ToWebP()
        {
            //Arrange
            MemoryStream jp2Stream = ConvertFileToMemoryStream("Documents/test.jp2");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.jp2)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(jp2Stream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJ2kToPng()
        {
            //Arrange
            MemoryStream j2kStream = ConvertFileToMemoryStream("Documents/test.j2k");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.j2k)
                                        .ThatConvertTo(FileExtension.png)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(j2kStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, PngFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJ2kToJpg()
        {
            //Arrange
            MemoryStream j2kStream = ConvertFileToMemoryStream("Documents/test.j2k");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.j2k)
                                        .ThatConvertTo(FileExtension.jpg)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(j2kStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, JpegFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJ2kToWebP()
        {
            //Arrange
            MemoryStream j2kStream = ConvertFileToMemoryStream("Documents/test.j2k");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.j2k)
                                        .ThatConvertTo(FileExtension.webp)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(j2kStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(IsImageFormatCorrect(result, WebpFormat.Instance));
        }

        [Fact]
        public async Task TestConvertingJp2ToPngReturnsStream()
        {
            //Arrange
            MemoryStream jp2Stream = ConvertFileToMemoryStream("Documents/test.jp2");

            //Act
            var result = await conversionService.ConvertJp2ToPng(jp2Stream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingJp2ToJpgReturnsStream()
        {
            //Arrange
            MemoryStream jp2Stream = ConvertFileToMemoryStream("Documents/test.jp2");

            //Act
            var result = await conversionService.ConvertJp2ToJpg(jp2Stream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Fact]
        public async Task TestConvertingJp2ToWebPReturnsStream()
        {
            //Arrange
            MemoryStream jp2Stream = ConvertFileToMemoryStream("Documents/test.jp2");

            //Act
            var result = await conversionService.ConvertJp2ToWebP(jp2Stream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".png")]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        [InlineData(".webp")]
        public void TestAvailableConversionsForJP2(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.jp2";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        [Theory]
        [InlineData(".png")]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        [InlineData(".webp")]
        public void TestAvailableConversionsForJ2K(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.j2k";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion JPEG 2000 (JP2/J2K) Conversion Tests

        #region Markdown to PDF Conversion Tests

        [Fact]
        public async Task TestConvertingMarkdownToPdf()
        {
            //Arrange
            MemoryStream mdStream = ConvertFileToMemoryStream("Documents/test.md");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.md)
                                        .ThatConvertTo(FileExtension.pdf)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(mdStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            result.Position = 0;
            var reader = new StreamReader(result, leaveOpen: true);
            var header = reader.ReadLine();
            Assert.StartsWith("%PDF", header);
        }

        [Fact]
        public async Task TestConvertingMarkdownToPdfReturnsStream()
        {
            //Arrange
            MemoryStream mdStream = ConvertFileToMemoryStream("Documents/test.md");

            //Act
            var result = await conversionService.ConvertMarkdownToPdf(mdStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);
        }

        #endregion Markdown to PDF Conversion Tests

        #region EPUB Conversion Tests

        [Fact]
        public async Task TestConvertingEpubToPdf()
        {
            //Arrange
            MemoryStream epubStream = ConvertFileToMemoryStream("Documents/test.epub");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.epub)
                                        .ThatConvertTo(FileExtension.pdf)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(epubStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            result.Position = 0;
            var reader = new StreamReader(result, leaveOpen: true);
            var header = reader.ReadLine();
            Assert.StartsWith("%PDF", header);
        }

        [Fact]
        public async Task TestConvertingEpubToPdfReturnsStream()
        {
            //Arrange
            MemoryStream epubStream = ConvertFileToMemoryStream("Documents/test.epub");

            //Act
            var result = await conversionService.ConvertEpubToPdf(epubStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public async Task TestConvertingEpubToTxt()
        {
            //Arrange
            MemoryStream epubStream = ConvertFileToMemoryStream("Documents/test.epub");

            var AvailableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.epub)
                                        .ThatConvertTo(FileExtension.txt)
                                        .FirstOrDefault();

            //Act
            var result = await AvailableConvertor.Convert(epubStream);

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var textContent = await reader.ReadToEndAsync();
            Assert.Contains("Test Chapter", textContent);
            Assert.Contains("test EPUB file", textContent);
        }

        [Fact]
        public async Task TestConvertingEpubToTxtReturnsStream()
        {
            //Arrange
            MemoryStream epubStream = ConvertFileToMemoryStream("Documents/test.epub");

            //Act
            var result = await conversionService.ConvertEpubToTxt(epubStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);
        }

        [Theory]
        [InlineData(".pdf")]
        [InlineData(".txt")]
        public void TestAvailableConversionsForEpub(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.epub";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion EPUB Conversion Tests

        #region DOCX Conversion Tests

        [Fact]
        public async Task TestConvertingDocxToPdfReturnsStream()
        {
            //Arrange
            var docxStream = ConvertFileToMemoryStream("Documents/Test Document.docx");

            //Act
            var result = await conversionService.ConvertDocxToPdf(docxStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);
            // Verify PDF header - PDF files start with %PDF
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var header = reader.ReadLine();
            Assert.StartsWith("%PDF", header);
        }

        [Fact]
        public async Task TestConvertingDocxToHtmlReturnsStream()
        {
            //Arrange
            var docxStream = ConvertFileToMemoryStream("Documents/Test Document.docx");

            //Act
            var result = await conversionService.ConvertDocxToHtml(docxStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);

            // Verify the HTML content
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var htmlContent = await reader.ReadToEndAsync();
            Assert.Contains("<!DOCTYPE html>", htmlContent);
            Assert.Contains("<html>", htmlContent);
        }

        [Fact]
        public async Task TestConvertingEmptyDocxToPdfThrowsException()
        {
            // Arrange - Create a minimal DOCX with no text content
            // The OpenXML SDK requires a valid structure, so we create a valid but empty document
            using var wordDoc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
                new MemoryStream(), DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            mainPart.Document.Body = new DocumentFormat.OpenXml.Wordprocessing.Body();
            // Body is empty - no paragraphs

            var emptyStream = new MemoryStream();
            wordDoc.Clone(emptyStream);
            emptyStream.Position = 0;

            // Act & Assert - Should throw ArgumentException for empty content
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await conversionService.ConvertDocxToPdf(emptyStream));
        }

        [Theory]
        [InlineData(".pdf")]
        [InlineData(".html")]
        public void TestAvailableConversionsForDocx(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.docx";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion DOCX Conversion Tests

        #region XLSX to PDF Conversion Tests

        [Fact]
        public async Task TestConvertingXlsxToPdfReturnsStream()
        {
            //Arrange
            var xlsxStream = ConvertFileToMemoryStream("Documents/test.xlsx");

            //Act
            var result = await conversionService.ConvertXlsxToPdf(xlsxStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(result.Length > 0);
            // Verify PDF header - PDF files start with %PDF
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var header = reader.ReadLine();
            Assert.StartsWith("%PDF", header);
        }

        [Fact]
        public async Task TestConvertingEmptyXlsxToPdfThrowsException()
        {
            // Arrange - Create an empty XLSX spreadsheet
            using var package = new ExcelPackage();
            package.Workbook.Worksheets.Add("Sheet1");
            // Don't add any data - worksheet.Dimension will be null
            var emptyStream = new MemoryStream(package.GetAsByteArray());

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await conversionService.ConvertXlsxToPdf(emptyStream));
        }

        [Theory]
        [InlineData(".pdf")]
        public void TestAvailableConversionsForXlsx(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.xlsx";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion XLSX to PDF Conversion Tests

        #region Helper Methods
        private static MemoryStream ConvertFileToMemoryStream(String FileName)
        {
            MemoryStream convertedStream = new MemoryStream();
            var fileToConvert = new FileInfo(FileName);
            using (FileStream file = new FileStream(fileToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.ReadExactly(bytes, 0, (int)file.Length);
                convertedStream.Write(bytes, 0, (int)file.Length);
            }
            return convertedStream;
        }
        static bool IsImageFormatCorrect(MemoryStream imageStream, IImageFormat expectedFormat)
        {
            try
            {
                imageStream.Position = 0;
                var detectedFormat = Image.DetectFormat(imageStream);
                return detectedFormat?.Name?.Equals(expectedFormat.Name, StringComparison.OrdinalIgnoreCase) == true;
            }
            catch
            {
                // Image.DetectFormat throws if the file does not have a valid image format
                return false;
            }
        }

        #endregion Helper Methods

        #region XML to YAML Conversion Tests

        [Fact]
        public async Task TestConvertingXmlToYaml()
        {
            // Arrange
            MemoryStream xmlStream = ConvertFileToMemoryStream("Documents/test.xml");

            var availableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.xml)
                                        .ThatConvertTo(FileExtension.yaml)
                                        .FirstOrDefault();

            // Act
            var result = await availableConvertor.Convert(xmlStream);

            // Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var yamlContent = await reader.ReadToEndAsync();
            Assert.Contains("name:", yamlContent);
            Assert.Contains("value:", yamlContent);
            Assert.Contains("Test", yamlContent);
            Assert.Contains("123", yamlContent);
        }

        [Fact]
        public async Task TestConvertingXmlToYml()
        {
            // Arrange
            MemoryStream xmlStream = ConvertFileToMemoryStream("Documents/test.xml");

            var availableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.xml)
                                        .ThatConvertTo(FileExtension.yml)
                                        .FirstOrDefault();

            // Act
            var result = await availableConvertor.Convert(xmlStream);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public async Task TestConvertingXmlToYamlReturnsStream()
        {
            // Arrange
            MemoryStream xmlStream = ConvertFileToMemoryStream("Documents/test.xml");

            // Act
            var result = await conversionService.ConvertXMLToYAML(xmlStream);

            // Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".yaml")]
        [InlineData(".yml")]
        public void TestAvailableConversionsForXmlToYaml(string conversionAvailable)
        {
            // Arrange
            var documentName = "testdoc.xml";

            // Act
            var result = conversionService.GetConvertorsForFile(documentName);

            // Assert
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion XML to YAML Conversion Tests

        #region YAML to XML Conversion Tests

        [Fact]
        public async Task TestConvertingYamlToXml()
        {
            // Arrange
            MemoryStream yamlStream = ConvertFileToMemoryStream("Documents/test.yaml");

            var availableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.yaml)
                                        .ThatConvertTo(FileExtension.xml)
                                        .FirstOrDefault();

            // Act
            var result = await availableConvertor.Convert(yamlStream);

            // Assert
            Assert.NotNull(result);
            result.Position = 0;
            using var reader = new StreamReader(result, leaveOpen: true);
            var xmlContent = await reader.ReadToEndAsync();
            Assert.Contains("<?xml", xmlContent);
            Assert.Contains("<Root>", xmlContent);
            Assert.Contains("name", xmlContent);
            Assert.Contains("Test Configuration", xmlContent);
        }

        [Fact]
        public async Task TestConvertingYmlToXml()
        {
            // Arrange
            MemoryStream yamlStream = ConvertFileToMemoryStream("Documents/test.yaml");

            var availableConvertor = conversionService.GetAllAvailableConvertors()
                                        .ThatConvertFrom(FileExtension.yml)
                                        .ThatConvertTo(FileExtension.xml)
                                        .FirstOrDefault();

            // Act
            var result = await availableConvertor.Convert(yamlStream);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        [Fact]
        public async Task TestConvertingYamlToXmlReturnsStream()
        {
            // Arrange
            MemoryStream yamlStream = ConvertFileToMemoryStream("Documents/test.yaml");

            // Act
            var result = await conversionService.ConvertYAMLToXML(yamlStream);

            // Assert
            Assert.IsType<MemoryStream>(result);
        }

        [Theory]
        [InlineData(".yaml")]
        [InlineData(".yml")]
        public void TestAvailableConversionsForYamlToXml(string extension)
        {
            // Arrange
            var documentName = $"testdoc{extension}";

            // Act
            var result = conversionService.GetConvertorsForFile(documentName);

            // Assert
            Assert.Contains(result, a => a.ConvertedExtension.Value == ".xml");
        }

        [Fact]
        public async Task TestConvertingInvalidXmlToYamlThrowsException()
        {
            // Arrange - Create invalid XML content
            var invalidXml = "<root><unclosed>";
            var xmlStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidXml));

            // Act & Assert
            await Assert.ThrowsAsync<System.Xml.XmlException>(async () =>
                await conversionService.ConvertXMLToYAML(xmlStream));
        }

        [Fact]
        public async Task TestConvertingInvalidYamlToXmlThrowsException()
        {
            // Arrange - Create invalid YAML content with unclosed bracket
            var invalidYaml = "key: [unclosed";
            var yamlStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidYaml));

            // Act & Assert - YamlDotNet throws SemanticErrorException for malformed YAML
            await Assert.ThrowsAsync<YamlDotNet.Core.SemanticErrorException>(async () =>
                await conversionService.ConvertYAMLToXML(yamlStream));
        }

        #endregion YAML to XML Conversion Tests
    }
}
