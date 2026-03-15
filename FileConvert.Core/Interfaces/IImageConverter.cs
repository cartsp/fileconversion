using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for basic image format conversions (JPG, PNG, GIF, WebP).
    /// </summary>
    public interface IImageConverter
    {
        Task<MemoryStream> ConvertToJpg(MemoryStream imageStream);
        Task<MemoryStream> ConvertToPng(MemoryStream imageStream);
        Task<MemoryStream> ConvertToGif(MemoryStream imageStream);
        Task<MemoryStream> ConvertToWebP(MemoryStream imageStream);
        Task<MemoryStream> ConvertWebPToJpg(MemoryStream webPStream);
        Task<MemoryStream> ConvertWebPToPng(MemoryStream webPStream);
        Task<MemoryStream> ConvertWebPToGif(MemoryStream webPStream);
        Task<MemoryStream> ConvertImageToIco(MemoryStream imageStream);
        Task<MemoryStream> ConvertIcoToPng(MemoryStream icoStream);
    }
}
