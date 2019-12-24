using FileConvert.Core;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml;
using System.Collections.Immutable;
using System.Linq;
using FileConvert.Core.Entities;
using NAudio.Wave;
using System;
using Microsoft.IdentityModel.Tokens;
using System.Drawing;
using System.Drawing.Imaging;

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
            //Todo add extension name lookup eg Excel 2007 = .xlsx
            Convertors = ConvertorListBuilder.ToImmutable();
        }

        public async Task<MemoryStream> ConvertDocToHTML(MemoryStream officeDocStream)
        {
            return officeDocStream;
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
            System.Drawing.Bitmap.FromStream(TifFile).Save(msPNG, System.Drawing.Imaging.ImageFormat.Png);

            return msPNG;
        }

        public async Task<MemoryStream> ConvertMP3ToWav(MemoryStream MP3Stream)
        {
            MemoryStream ConvertedWaveStream = new MemoryStream();

            //    using (var reader = new Mp3FileReader(officeDocStream)
            //    {
            //        WaveFileWriter.WriteWavFileToStream(ConvertedWaveFile, reader.Mp3WaveFormat);
            //    }
            //}
            ///MP3Stream.Position = 0;
            //var base64File = Encoding.ASCII.GetString(MP3Stream.ToArray());
            //var fileBytes = Encoding.UTF8.GetBytes(Base64UrlEncoder.Decode(Encoding.ASCII.GetString(MP3Stream.ToArray())));
            ///ar fileBytes = Convert.FromBase64String(base64File);
            using (WaveStream waveStream = WaveFormatConversionStream.CreatePcmStream(new Mp3FileReader(MP3Stream)))
            using (WaveFileWriter waveFileWriter = new WaveFileWriter(ConvertedWaveStream, waveStream.WaveFormat))
            {
                byte[] bytes = new byte[waveStream.Length];
                waveStream.Position = 0;
                await waveStream.ReadAsync(bytes, 0, (int)waveStream.Length);
                await waveFileWriter.WriteAsync(bytes, 0, bytes.Length);
                waveFileWriter.Flush();
                ConvertedWaveStream.Position = 0;

                return ConvertedWaveStream;
            }
        }

        public async Task<MemoryStream> ConvertCSVToExcel(MemoryStream CSVStream)
        {
            var csvFile= Encoding.ASCII.GetString(CSVStream.ToArray());

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sheet1");

                worksheet.Cells["A1"].LoadFromText(csvFile, null);

                return new MemoryStream(package.GetAsByteArray());
            }
        }
        public IImmutableList<ConvertorDetails> GetAvailableConversions(string inputFileName)
        {
            return Convertors.Where(cd => cd.ExtensionToConvert == Path.GetExtension(inputFileName)).ToImmutableList();
        }

        public IImmutableList<ConvertorDetails> GetCompatibleExtensions()
        {
            return Convertors;
        }
    }
}
