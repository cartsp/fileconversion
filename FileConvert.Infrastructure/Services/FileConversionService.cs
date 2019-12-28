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
using System;

namespace FileConvert.Infrastructure
{
    public class FileConversionService : IFileConvertors
    {
        public static IImmutableList<ConvertorDetails> Convertors;

        public FileConversionService()
        {
            CreateConvertorList();
        }

        public void CreateConvertorList()
        {
            var ConvertorListBuilder = ImmutableList.CreateBuilder<ConvertorDetails>(); // returns ImmutableList.Builder
            
            ConvertorListBuilder.Add(new ConvertorDetails(".csv", ".xls", ConvertCSVToExcel));
            ConvertorListBuilder.Add(new ConvertorDetails(".csv", ".xlsx", ConvertCSVToExcel));
            ConvertorListBuilder.Add(new ConvertorDetails(".docx", ".html", ConvertDocToHTML));
            ConvertorListBuilder.Add(new ConvertorDetails(".mp3", ".wav", ConvertMP3ToWav));
            ConvertorListBuilder.Add(new ConvertorDetails(".tif", ".png", ConverTifToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(".png", ".jpg", ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(".png", ".jpeg", ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(".gif", ".jpeg", ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(".gif", ".jpg", ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(".bmp", ".jpeg", ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(".bmp", ".jpg", ConvertImageTojpg));
            ConvertorListBuilder.Add(new ConvertorDetails(".png", ".gif", ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(".jpg", ".gif", ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(".jpeg", ".gif", ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(".bmp", ".gif", ConvertImageToGIF));
            ConvertorListBuilder.Add(new ConvertorDetails(".gif", ".png", ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(".jpg", ".png", ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(".jpeg", ".png", ConvertImageToPNG));
            ConvertorListBuilder.Add(new ConvertorDetails(".bmp", ".png", ConvertImageToPNG));
            //ConvertorListBuilder.Add(new ConvertorDetails(".png", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".gif", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".jpg", ".bmp", ConvertImageToBMP));
            //ConvertorListBuilder.Add(new ConvertorDetails(".jpeg", ".bmp", ConvertImageToBMP));
            
            Convertors = ConvertorListBuilder.ToImmutable();
        }

        public async Task<MemoryStream> ConvertDocToHTML(MemoryStream officeDocStream)
        {
            return await Task.FromResult(officeDocStream);
        }   
        
        public async Task<MemoryStream> ConvertImageTojpg(MemoryStream PNGStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image<Rgba32> image = Image.Load<Rgba32>(PNGStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, new JpegEncoder() { Quality = 80 });
            }

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertImageToPNG(MemoryStream ImageStream)
        {
            MemoryStream outputStream = new MemoryStream();

            using (Image<Rgba32> image = Image.Load<Rgba32>(ImageStream.ToArray()))
            {
                image.SaveAsPng(outputStream);
            }

            return await Task.FromResult(outputStream);
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
            var csvFile= Encoding.ASCII.GetString(CSVStream.ToArray());

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sheet1");

                worksheet.Cells["A1"].LoadFromText(csvFile, null);

                return await Task.FromResult(new MemoryStream(package.GetAsByteArray()));
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
