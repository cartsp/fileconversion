using FileConvert.Core.ValueObjects;
using FileConvert.Infrastructure;
using System;
using System.Drawing.Imaging;
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
            var result = conversionService.GetConvertorsForFile(DocumentName);

            //Assert
            Assert.True(result.Count != 0);
            Assert.True(result.Count == 2);
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
            Assert.Equal(16, result.Count);
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

        [Fact]
        public async Task TestConvertingCSVToExcelReturnsStream()
        {
            //Arrange
            var officeDocStream = ConvertFileToMemoryStream("Documents/Untitled 1.csv");

            //Act
            var result = await conversionService.ConvertCSVToExcel(officeDocStream);

            //Assert
            Assert.IsType<MemoryStream>(result);
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Jpeg));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Gif));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Jpeg));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Png));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Png));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Gif));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Gif));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Jpeg));
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
            Assert.True(IsImageFormatCorrect(result, ImageFormat.Png));
        }

        #endregion Image tests

        #region Helper Methods

        private static MemoryStream ConvertFileToMemoryStream(String FileName)
        {
            MemoryStream convertedStream = new MemoryStream();
            var fileToConvert = new FileInfo(FileName);

            using (FileStream file = new FileStream(fileToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                convertedStream.Write(bytes, 0, (int)file.Length);
            }

            return convertedStream;
        }

        static bool IsImageFormatCorrect(MemoryStream gif, ImageFormat format)
        {
            try
            {
                using (System.Drawing.Image img = System.Drawing.Image.FromStream(gif))
                {
                    // Two image formats can be compared using the Equals method
                    // See http://msdn.microsoft.com/en-us/library/system.drawing.imaging.imageformat.aspx
                    //
                    return img.RawFormat.Equals(format);
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
        
        #endregion Helper Methods
    }
}
