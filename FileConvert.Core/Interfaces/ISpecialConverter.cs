using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for special conversions (QR codes, barcodes).
    /// </summary>
    public interface ISpecialConverter
    {
        Task<MemoryStream> ConvertTextToQrCodePng(MemoryStream textStream);
        Task<MemoryStream> ConvertTextToBarcodeJpg(MemoryStream textStream);
    }
}
