using FileConvert.Core.ValueObjects;
using FileConvert.Infrastructure;
using OfficeOpenXml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

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
            Assert.True(result.Count == 1);
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
            Assert.Equal(21, result.Count);
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
            Assert.True(result.Count == 1);
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
        public void TestAvailableConversionsForMarkdown(string conversionAvailable)
        {
            //Arrange
            var DocumentName = "testdoc.md";

            //Act
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 1);
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
            Assert.True(result.Count == 1);
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
            Assert.True(result.Count == 1);
            Assert.Contains(result, a => a.ConvertedExtension.Value == conversionAvailable);
        }

        #endregion XLSX to CSV tests

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
    }
}
