using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for document format conversions (DOCX, XLSX, PPTX, EPUB, Markdown).
    /// </summary>
    public interface IDocumentConverter
    {
        // DOCX conversions
        Task<MemoryStream> ConvertDocxToPdf(MemoryStream docxStream);
        Task<MemoryStream> ConvertDocxToHtml(MemoryStream docxStream);

        // XLSX conversions
        Task<MemoryStream> ConvertXlsxToCsv(MemoryStream xlsxStream);
        Task<MemoryStream> ConvertXlsxToJson(MemoryStream xlsxStream);
        Task<MemoryStream> ConvertXlsxToPdf(MemoryStream xlsxStream);

        // PPTX conversions
        Task<MemoryStream> ConvertPptxToPdf(MemoryStream pptxStream);
        Task<MemoryStream> ConvertPptxToPng(MemoryStream pptxStream);
        Task<MemoryStream> ConvertPptxToJpg(MemoryStream pptxStream);

        // Markdown conversions
        Task<MemoryStream> ConvertMarkdownToHtml(MemoryStream markdownStream);
        Task<MemoryStream> ConvertMarkdownToPdf(MemoryStream markdownStream);

        // EPUB conversions
        Task<MemoryStream> ConvertEpubToPdf(MemoryStream epubStream);
        Task<MemoryStream> ConvertEpubToTxt(MemoryStream epubStream);

        // HTML conversions
        Task<MemoryStream> ConvertHtmlToText(MemoryStream htmlStream);
        Task<MemoryStream> ConvertHtmlToPdf(MemoryStream htmlStream);

        // RTF conversions
        Task<MemoryStream> ConvertRtfToHtml(MemoryStream rtfStream);
        Task<MemoryStream> ConvertRtfToTxt(MemoryStream rtfStream);

        // OpenDocument conversions
        Task<MemoryStream> ConvertOdtToDocx(MemoryStream odtStream);
        Task<MemoryStream> ConvertOdsToXlsx(MemoryStream odsStream);
    }
}
