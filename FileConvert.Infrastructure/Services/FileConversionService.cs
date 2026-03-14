using FileConvert.Core;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Linq;
using FileConvert.Core.Entities;
//using NAudio.Wave;
using ClosedXML.Excel;
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
            
            Convertors = ConvertorListBuilder.ToImmutable();
        }

        public async Task<MemoryStream> ConvertDocToHTML(MemoryStream officeDocStream)
        {
            ArgumentNullException.ThrowIfNull(officeDocStream);
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
            ArgumentNullException.ThrowIfNull(PNGStream);

            MemoryStream outputStream = new MemoryStream();

            using (Image<Rgba32> image = Image.Load<Rgba32>(PNGStream.ToArray()))
            {
                image.SaveAsJpeg(outputStream, new JpegEncoder() { Quality = 80 });
            }

            return await Task.FromResult(outputStream).ConfigureAwait(true);
        }

        public async Task<MemoryStream> ConvertImageToPNG(MemoryStream ImageStream)
        {
            ArgumentNullException.ThrowIfNull(ImageStream);

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
            ArgumentNullException.ThrowIfNull(ImageStream);

            MemoryStream outputStream = new MemoryStream();

            using (Image<Rgba32> image = Image.Load<Rgba32>(ImageStream.ToArray()))
            {
                image.SaveAsGif(outputStream);
            }

            return await Task.FromResult(outputStream).ConfigureAwait(true);
        }


        public Task<MemoryStream> ConverTifToPNG(MemoryStream TifFile)
        {
            ArgumentNullException.ThrowIfNull(TifFile);
            throw new NotImplementedException("TIF to PNG conversion is not yet implemented.");
        }

        public Task<MemoryStream> ConvertMP3ToWav(MemoryStream MP3Stream)
        {
            ArgumentNullException.ThrowIfNull(MP3Stream);
            throw new NotImplementedException("MP3 to WAV conversion is not yet implemented.");
        }

        public async Task<MemoryStream> ConvertCSVToExcel(MemoryStream CSVStream)
        {
            ArgumentNullException.ThrowIfNull(CSVStream);

            var csvContent = Encoding.UTF8.GetString(CSVStream.ToArray());
            var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Sheet1");

                for (int row = 0; row < lines.Length; row++)
                {
                    var columns = ParseCsvLine(lines[row]);
                    for (int col = 0; col < columns.Length; col++)
                    {
                        worksheet.Cell(row + 1, col + 1).Value = columns[col];
                    }
                }

                var outputStream = new MemoryStream();
                workbook.SaveAs(outputStream);
                outputStream.Position = 0;
                return await Task.FromResult(outputStream).ConfigureAwait(true);
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new System.Collections.Generic.List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            result.Add(currentField.ToString());

            return result.ToArray();
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
