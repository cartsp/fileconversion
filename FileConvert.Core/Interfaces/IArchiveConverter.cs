using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for archive format conversions (GZ, TAR, BZ2, ZIP, 7Z, RAR).
    /// </summary>
    public interface IArchiveConverter
    {
        // GZ/TGZ conversions
        Task<MemoryStream> ConvertGzToTar(MemoryStream gzStream);
        Task<MemoryStream> ConvertTarToGz(MemoryStream tarStream);

        // BZ2/TBZ2 conversions
        Task<MemoryStream> ConvertBz2ToTar(MemoryStream bz2Stream);

        // ZIP conversions
        Task<MemoryStream> ConvertZipToTar(MemoryStream zipStream);
        Task<MemoryStream> ConvertTarToZip(MemoryStream tarStream);

        // 7Z conversions
        Task<MemoryStream> Convert7zToZip(MemoryStream sevenZipStream);
        Task<MemoryStream> Convert7zToTar(MemoryStream sevenZipStream);

        // RAR conversions
        Task<MemoryStream> ConvertRarToZip(MemoryStream rarStream);
        Task<MemoryStream> ConvertRarToTar(MemoryStream rarStream);
    }
}
