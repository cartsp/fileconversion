using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for TIFF format conversions.
    /// </summary>
    public interface ITiffConverter
    {
        Task<MemoryStream> ConvertToPng(MemoryStream tiffStream);
        Task<MemoryStream> ConvertToJpg(MemoryStream tiffStream);
        Task<MemoryStream> ConvertToWebP(MemoryStream tiffStream);
    }
}
