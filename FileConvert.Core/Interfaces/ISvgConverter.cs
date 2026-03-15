using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for SVG format conversions.
    /// </summary>
    public interface ISvgConverter
    {
        Task<MemoryStream> ConvertToPng(MemoryStream svgStream);
        Task<MemoryStream> ConvertToJpg(MemoryStream svgStream);
        Task<MemoryStream> ConvertToWebP(MemoryStream svgStream);
    }
}
