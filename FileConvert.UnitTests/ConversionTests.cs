using FileConvert.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FileConvert.UnitTests
{
    public class ConversionTests
    {
        public static FileConversionService conversionService = new FileConversionService();
        [Theory]
        [InlineData(".xls")]
        [InlineData(".xlsx")]
        public void TestAvailableConversionsForCSV(string conversionAvailable)
        {
            //Arrange
            MemoryStream officeDocStream = new MemoryStream();
            var DocumentName = "testdoc.csv";

            //Act
            var result = conversionService.GetAvailableConversions(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
            Assert.Contains(result, a => a.ConvertedExtension == conversionAvailable);
        }

        [Fact]
        public void TestGetAllAvailableConversions()
        {
            //Arrange
            MemoryStream officeDocStream = new MemoryStream();

            //Act
            var result = conversionService.GetCompatibleExtensions();

            //Assert
            Assert.NotNull(result);
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 4);
        }

        [Fact]
        public async Task TestConvertingWordDocToHTMLReturnsStream()
        {
            //Arrange
            MemoryStream officeDocStream = new MemoryStream();
            var wordDocToConvert = new FileInfo("Documents/Test Document.docx");

            using (FileStream file = new FileStream(wordDocToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                officeDocStream.Write(bytes, 0, (int)file.Length);
            }

            //Act
            var result = await conversionService.ConvertDocToHTML(officeDocStream);

            //Assert
            Assert.IsType<MemoryStream>(result);

        }

        [Fact]
        public async Task TestConvertingCSVToExcelReturnsStream()
        {
            //Arrange
            MemoryStream officeDocStream = new MemoryStream();
            var wordDocToConvert = new FileInfo("Documents/Untitled 1.csv");

            using (FileStream file = new FileStream(wordDocToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                officeDocStream.Write(bytes, 0, (int)file.Length);
            }

            //Act
            var result = await conversionService.ConvertCSVToExcel(officeDocStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
        }
    }
}
