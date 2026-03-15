using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for modern image format conversions (HEIC, AVIF, JXL, DNG, JP2/J2K).
    /// </summary>
    public interface IModernImageConverter
    {
        // HEIC/HEIF conversions
        Task<MemoryStream> ConvertHeicToJpg(MemoryStream heicStream);
        Task<MemoryStream> ConvertHeicToPng(MemoryStream heicStream);
        Task<MemoryStream> ConvertHeicToWebP(MemoryStream heicStream);

        // AVIF conversions
        Task<MemoryStream> ConvertAvifToJpg(MemoryStream avifStream);
        Task<MemoryStream> ConvertAvifToPng(MemoryStream avifStream);
        Task<MemoryStream> ConvertAvifToWebP(MemoryStream avifStream);

        // JPEG XL (JXL) conversions
        Task<MemoryStream> ConvertJxlToJpg(MemoryStream jxlStream);
        Task<MemoryStream> ConvertJxlToPng(MemoryStream jxlStream);
        Task<MemoryStream> ConvertJxlToWebP(MemoryStream jxlStream);

        // DNG conversions
        Task<MemoryStream> ConvertDngToJpg(MemoryStream dngStream);
        Task<MemoryStream> ConvertDngToPng(MemoryStream dngStream);
        Task<MemoryStream> ConvertDngToWebP(MemoryStream dngStream);

        // JPEG 2000 (JP2/J2K) conversions
        Task<MemoryStream> ConvertJp2ToPng(MemoryStream jp2Stream);
        Task<MemoryStream> ConvertJp2ToJpg(MemoryStream jp2Stream);
        Task<MemoryStream> ConvertJp2ToWebP(MemoryStream jp2Stream);
    }
}
