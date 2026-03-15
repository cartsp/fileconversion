using System.IO;
using System.Threading.Tasks;

namespace FileConvert.Core.Interfaces
{
    /// <summary>
    /// Interface for data format conversions (CSV, JSON, XML, YAML, TSV).
    /// </summary>
    public interface IDataConverter
    {
        // CSV conversions
        Task<MemoryStream> ConvertCsvToXlsx(MemoryStream csvStream);
        Task<MemoryStream> ConvertCsvToJson(MemoryStream csvStream);
        Task<MemoryStream> ConvertCsvToYaml(MemoryStream csvStream);

        // JSON conversions
        Task<MemoryStream> ConvertJsonToXml(MemoryStream jsonStream);
        Task<MemoryStream> ConvertJsonToCsv(MemoryStream jsonStream);
        Task<MemoryStream> ConvertJsonToYaml(MemoryStream jsonStream);

        // XML conversions
        Task<MemoryStream> ConvertXmlToJson(MemoryStream xmlStream);
        Task<MemoryStream> ConvertXmlToCsv(MemoryStream xmlStream);
        Task<MemoryStream> ConvertXmlToYaml(MemoryStream xmlStream);

        // YAML conversions
        Task<MemoryStream> ConvertYamlToJson(MemoryStream yamlStream);
        Task<MemoryStream> ConvertYamlToXml(MemoryStream yamlStream);

        // TSV conversions
        Task<MemoryStream> ConvertTsvToCsv(MemoryStream tsvStream);
        Task<MemoryStream> ConvertTsvToJson(MemoryStream tsvStream);
    }
}
