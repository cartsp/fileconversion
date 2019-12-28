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
            Assert.Equal(19, result.Count);
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

        [Fact]
        public async Task TestConvertingPNGToJPG()
        {
            //Arrange
            MemoryStream pngStream = new MemoryStream();
            var pngToConvert = new FileInfo("Documents/small-png-image.png");

            using (FileStream file = new FileStream(pngToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                pngStream.Write(bytes, 0, (int)file.Length);
            }

            //Act
            var result = await conversionService.ConvertImageTojpg(pngStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(IsJpegImage(result));
        }

        [Fact]
        public async Task TestConvertingGIFToJPG()
        {
            //Arrange
            MemoryStream gifStream = new MemoryStream();
            var gifToConvert = new FileInfo("Documents/sample.gif");

            using (FileStream file = new FileStream(gifToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                gifStream.Write(bytes, 0, (int)file.Length);
            }

            //Act
            var result = await conversionService.ConvertImageTojpg(gifStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
            Assert.True(IsJpegImage(result));
        }

        static bool IsJpegImage(MemoryStream jpg)
        {
            try
            {
                using (System.Drawing.Image img = System.Drawing.Image.FromStream(jpg))
                {
                    // Two image formats can be compared using the Equals method
                    // See http://msdn.microsoft.com/en-us/library/system.drawing.imaging.imageformat.aspx
                    //
                    return img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg);
                }
            }
            catch (OutOfMemoryException)
            {
                // Image.FromFile throws an OutOfMemoryException 
                // if the file does not have a valid image format or
                // GDI+ does not support the pixel format of the file.
                //
                return false;
            }
        }
    }
}
