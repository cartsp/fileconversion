using FileConvert.Infrastructure;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Infrastruture.Tester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var conversionService = new FileConversionService();

            MemoryStream pngStream = new MemoryStream();
            var wordDocToConvert = new FileInfo("test.pdf");
            //var wordDocToConvert = new FileInfo("addresses.csv");

            using (FileStream file = new FileStream(wordDocToConvert.FullName, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                pngStream.Write(bytes, 0, (int)file.Length);
                pngStream.Position = 0;
                var result = await conversionService.ConvertPDFToDocx(pngStream);
                
                //FileStream jpgfile = new FileStream("file.xls", FileMode.Create, FileAccess.Write);
                FileStream jpgfile = new FileStream("test.docx", FileMode.Create, FileAccess.Write);
                result.WriteTo(jpgfile);
                jpgfile.Close();
                result.Close();
                //do csv to make sure its what i think
            }
        }
    }
}
public static class Extensions
{
    public static MemoryStream ConvertToBase64(this MemoryStream stream)
    {
        byte[] bytes;
        using (var memoryStream = new MemoryStream())
        {
            stream.CopyTo(memoryStream);
            bytes = memoryStream.ToArray();
        }

        string base64 = Convert.ToBase64String(bytes);
        return new MemoryStream(Encoding.UTF8.GetBytes(base64));
    }
}

