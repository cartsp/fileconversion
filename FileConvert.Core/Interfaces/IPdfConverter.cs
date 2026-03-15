using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for PDF-related conversions.
    /// </summary>
    public interface IPdfConverter
    {
        Task<MemoryStream> ConvertImageToPdf(MemoryStream imageStream);
        Task<MemoryStream> ConvertPdfToPng(MemoryStream pdfStream);
        Task<MemoryStream> ConvertPdfToJpg(MemoryStream pdfStream);
        Task<MemoryStream> ConvertPdfToText(MemoryStream pdfStream);
        Task<MemoryStream> MergePdfs(List<MemoryStream> pdfStreams);
        Task<MemoryStream> ExtractPage(MemoryStream pdfStream, int pageNumber);
    }
}
