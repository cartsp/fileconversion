using FileConvert.Core;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Linq;
using FileConvert.Core.Entities;
//using NAudio.Wave;
using OfficeOpenXml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using FileConvert.Core.ValueObjects;
using System.Globalization;
using System.Threading;

namespace FileConvert.Infrastructure
{
    public class FileConversionService : IFileConvertors
    {
        static IImmutableList<ConvertorDetails> Convertors;

        public FileConversionService()
        {
            CreateConvertorList();
        }

        public void CreateConvertorList()
        {
            var ConvertorListBuilder = ImmutableList.CreateBuilder<ConvertorDetails>(); // returns ImmutableList.Builder
            
            //ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.xls, ConvertCSVToExcel));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.xlsx, ConvertCSVToExcel));
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
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.gif, ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.png, ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.png, ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.png, ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.png, ConvertImageToPNG));
            //ConvertorListBuilder.Add(new ConvertorDetails(".png", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".gif", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".jpg", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".jpeg", ".bmp", ConvertImageToBMP));
            
            Convertors = ConvertorListBuilder.ToImmutable();
        }

        public async Task<MemoryStream> ConvertDocToHTML(MemoryStream officeDocStream)
        {
            return await Task.FromResult(officeDocStream).ConfigureAwait(true);
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
        //        return await Task.FromResult(pdfStream).ConfigureAwait(true);
        //    }
        //}   

        public async Task<MemoryStream> ConvertImageTojpg(MemoryStream PNGStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image<Rgba32> image = Image.Load<Rgba32>(PNGStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, new JpegEncoder() { Quality = 80 });
            }

            return await Task.FromResult(outputStream).ConfigureAwait(true);
        }

        public async Task<MemoryStream> ConvertImageToPNG(MemoryStream ImageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image<Rgba32> image = Image.Load<Rgba32>(ImageStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return await Task.FromResult(outputStream).ConfigureAwait(true);
        }

        //public async Task<MemoryStream> ConvertImageToBMP(MemoryStream PNGStream)
        //{
        //    MemoryStream outputStream = new MemoryStream();

        //    using (Image<Rgba32> image = Image.Load<Rgba32>(PNGStream.ToArray()))
        //    {
        //        image.SaveAsBmp(outputStream);
        //    }

        //    return await Task.FromResult(outputStream);
        //}

        public async Task<MemoryStream> ConvertImageToGIF(MemoryStream ImageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image<Rgba32> image = Image.Load<Rgba32>(ImageStream.ToArray()))
            {
                image.SaveAsGif(outputStream);
            }

            return await Task.FromResult(outputStream).ConfigureAwait(true);
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
            
            return await Task.FromResult(msPNG).ConfigureAwait(true);
        }

        public async Task<MemoryStream> ConvertMP3ToWav(MemoryStream MP3Stream)
        {
            MemoryStream ConvertedWaveStream = new MemoryStream();

            
            return await Task.FromResult(ConvertedWaveStream).ConfigureAwait(true);
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

                return await Task.FromResult(new MemoryStream(package.GetAsByteArray())).ConfigureAwait(true);
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
