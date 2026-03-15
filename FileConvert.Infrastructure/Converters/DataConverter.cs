using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using FileConvert.Core.Interfaces;
using OfficeOpenXml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles data format conversions (CSV, JSON, XML, YAML, TSV).
    /// </summary>
    public class DataConverter : IDataConverter
    {
        private static readonly JsonSerializerOptions CachedJsonOptions = new JsonSerializerOptions { WriteIndented = true };

        // Secure YAML deserializer that doesn't allow arbitrary type instantiation
        // By not calling .WithTagMapping(), we prevent !type tags from instantiating arbitrary types
        private static readonly IDeserializer CachedYamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        private static readonly ISerializer CachedYamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        private const long MaxUncompressedSize = 1024 * 1024 * 500; // 500MB max
        private const int MaxXmlEntityExpansion = 1000; // Prevent billion laughs attack

        static DataConverter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        #region CSV Conversions

        public async Task<MemoryStream> ConvertCsvToXlsx(MemoryStream csvStream)
        {
            ExcelTextFormat format = new ExcelTextFormat
            {
                Delimiter = ',',
                Encoding = new UTF8Encoding(),
                EOL = "\n"
            };

            var csvFile = Encoding.ASCII.GetString(csvStream.ToArray());

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Sheet1");
                worksheet.Cells["A1"].LoadFromText(csvFile, format);
                return await Task.FromResult(new MemoryStream(package.GetAsByteArray()));
            }
        }

        public async Task<MemoryStream> ConvertCsvToJson(MemoryStream csvStream)
        {
            var csvContent = Encoding.UTF8.GetString(csvStream.ToArray());
            using var reader = new StringReader(csvContent);

            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
                return await WriteStringToStreamAsync("[]");

            var headers = ParseCsvLine(headerLine);
            var rows = new List<Dictionary<string, object>>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line);
                var rowData = new Dictionary<string, object>();

                for (int i = 0; i < headers.Count; i++)
                {
                    var value = i < values.Count ? values[i] : string.Empty;
                    rowData[headers[i]] = ConvertCsvValueToJson(value);
                }

                rows.Add(rowData);
            }

            return await WriteStringToStreamAsync(JsonSerializer.Serialize(rows, CachedJsonOptions));
        }

        public async Task<MemoryStream> ConvertCsvToYaml(MemoryStream csvStream)
        {
            var csvContent = Encoding.UTF8.GetString(csvStream.ToArray());
            using var reader = new StringReader(csvContent);

            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
                return await WriteStringToStreamAsync(string.Empty);

            var headers = ParseCsvLine(headerLine);
            var rows = new List<Dictionary<string, object>>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line);
                var rowData = new Dictionary<string, object>();

                for (int i = 0; i < headers.Count; i++)
                {
                    var value = i < values.Count ? values[i] : string.Empty;
                    rowData[headers[i]] = ConvertCsvValueToJson(value);
                }

                rows.Add(rowData);
            }

            var yamlContent = CachedYamlSerializer.Serialize(rows);
            return await WriteStringToStreamAsync(yamlContent);
        }

        #endregion

        #region JSON Conversions

        public async Task<MemoryStream> ConvertJsonToXml(MemoryStream jsonStream)
        {
            var jsonString = Encoding.UTF8.GetString(jsonStream.ToArray());
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            var xmlRoot = new XElement("Root");
            ConvertJsonElementToXml(root, xmlRoot);

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(xmlRoot.ToString());
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertJsonToCsv(MemoryStream jsonStream)
        {
            var jsonContent = Encoding.UTF8.GetString(jsonStream.ToArray());
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                return await WriteStringToStreamAsync(string.Empty);

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                var firstItem = root[0];
                var headers = new List<string>();
                foreach (var property in firstItem.EnumerateObject())
                {
                    headers.Add(property.Name);
                }
                writer.WriteLine(string.Join(",", headers.Select(EscapeCsvField)));

                foreach (var item in root.EnumerateArray())
                {
                    var values = new List<string>();
                    foreach (var header in headers)
                    {
                        if (item.TryGetProperty(header, out var prop))
                        {
                            values.Add(EscapeCsvField(ConvertJsonElementToCsvValue(prop)));
                        }
                        else
                        {
                            values.Add(string.Empty);
                        }
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertJsonToYaml(MemoryStream jsonStream)
        {
            var jsonContent = Encoding.UTF8.GetString(jsonStream.ToArray());

            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            var yamlObject = ConvertJsonElementToYamlObject(root);
            var yamlContent = CachedYamlSerializer.Serialize(yamlObject);

            return await WriteStringToStreamAsync(yamlContent);
        }

        #endregion

        #region XML Conversions

        public async Task<MemoryStream> ConvertXmlToJson(MemoryStream xmlStream)
        {
            var xmlString = Encoding.UTF8.GetString(xmlStream.ToArray());
            var xdoc = ParseXmlSecurely(xmlString);

            var jsonResult = ConvertXmlElementToJson(xdoc.Root);

            return await WriteStringToStreamAsync(JsonSerializer.Serialize(jsonResult, CachedJsonOptions));
        }

        public async Task<MemoryStream> ConvertXmlToCsv(MemoryStream xmlStream)
        {
            var xmlString = Encoding.UTF8.GetString(xmlStream.ToArray());
            var xdoc = ParseXmlSecurely(xmlString);

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                var rows = xdoc.Root?.Elements().ToList() ?? new List<XElement>();

                if (rows.Count == 0)
                    return await WriteStringToStreamAsync(string.Empty);

                var headers = new HashSet<string>();
                foreach (var row in rows)
                {
                    foreach (var element in row.Elements())
                    {
                        headers.Add(element.Name.LocalName);
                    }
                }
                var headerList = headers.ToList();

                writer.WriteLine(string.Join(",", headerList.Select(EscapeCsvField)));

                foreach (var row in rows)
                {
                    var values = new List<string>();
                    foreach (var header in headerList)
                    {
                        var element = row.Element(header);
                        values.Add(EscapeCsvField(element?.Value ?? string.Empty));
                    }
                    writer.WriteLine(string.Join(",", values));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertXmlToYaml(MemoryStream xmlStream)
        {
            if (xmlStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input XML exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            var xmlString = Encoding.UTF8.GetString(xmlStream.ToArray());
            var xdoc = ParseXmlSecurely(xmlString);

            if (xdoc.Root == null)
                return await WriteStringToStreamAsync(string.Empty);

            var jsonResult = ConvertXmlElementToJson(xdoc.Root);
            var yamlContent = CachedYamlSerializer.Serialize(jsonResult);
            return await WriteStringToStreamAsync(yamlContent);
        }

        #endregion

        #region YAML Conversions

        public async Task<MemoryStream> ConvertYamlToJson(MemoryStream yamlStream)
        {
            var yamlContent = Encoding.UTF8.GetString(yamlStream.ToArray());
            var yamlObject = CachedYamlDeserializer.Deserialize(yamlContent);
            return await WriteStringToStreamAsync(JsonSerializer.Serialize(yamlObject, CachedJsonOptions));
        }

        public async Task<MemoryStream> ConvertYamlToXml(MemoryStream yamlStream)
        {
            if (yamlStream.Length > MaxUncompressedSize)
                throw new InvalidOperationException($"Input YAML exceeds maximum allowed size of {MaxUncompressedSize / (1024 * 1024)}MB");

            var yamlContent = Encoding.UTF8.GetString(yamlStream.ToArray());
            var yamlObject = CachedYamlDeserializer.Deserialize(yamlContent);

            var rootElement = new XElement("Root");
            ConvertObjectToXml(yamlObject, rootElement);

            var xmlString = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>{Environment.NewLine}{rootElement}";
            return await WriteStringToStreamAsync(xmlString);
        }

        #endregion

        #region TSV Conversions

        public async Task<MemoryStream> ConvertTsvToCsv(MemoryStream tsvStream)
        {
            var tsvContent = Encoding.UTF8.GetString(tsvStream.ToArray());

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                using var reader = new StringReader(tsvContent);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var fields = line.Split('\t');
                    var csvFields = fields.Select(EscapeCsvField);
                    writer.WriteLine(string.Join(",", csvFields));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertTsvToJson(MemoryStream tsvStream)
        {
            var tsvContent = Encoding.UTF8.GetString(tsvStream.ToArray());
            using var reader = new StringReader(tsvContent);

            var headerLine = reader.ReadLine();
            if (string.IsNullOrEmpty(headerLine))
                return await WriteStringToStreamAsync("[]");

            var headers = headerLine.Split('\t');
            var rows = new List<Dictionary<string, object>>();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = line.Split('\t');
                var rowData = new Dictionary<string, object>();

                for (int i = 0; i < headers.Length; i++)
                {
                    var value = i < values.Length ? values[i] : string.Empty;
                    rowData[headers[i]] = ConvertCsvValueToJson(value);
                }

                rows.Add(rowData);
            }

            return await WriteStringToStreamAsync(JsonSerializer.Serialize(rows, CachedJsonOptions));
        }

        #endregion

        #region Helper Methods

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        private static async Task<MemoryStream> WriteStringToStreamAsync(string content)
        {
            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                await writer.WriteAsync(content);
            }
            outputStream.Position = 0;
            return outputStream;
        }

        private List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            fields.Add(currentField.ToString());
            return fields;
        }

        private object ConvertCsvValueToJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                return longValue;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                return doubleValue;

            if (bool.TryParse(value, out var boolValue))
                return boolValue;

            return value;
        }

        private string ConvertJsonElementToCsvValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => element.ToString()
            };
        }

        private void ConvertJsonElementToXml(JsonElement jsonElement, XElement parent)
        {
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in jsonElement.EnumerateObject())
                    {
                        var element = new XElement(property.Name);
                        parent.Add(element);
                        ConvertJsonElementToXml(property.Value, element);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        var element = new XElement("Item");
                        parent.Add(element);
                        ConvertJsonElementToXml(item, element);
                    }
                    break;

                case JsonValueKind.String:
                    parent.Value = jsonElement.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Number:
                    parent.Value = jsonElement.ToString();
                    break;

                case JsonValueKind.True:
                    parent.Value = "true";
                    break;

                case JsonValueKind.False:
                    parent.Value = "false";
                    break;

                case JsonValueKind.Null:
                    parent.Value = string.Empty;
                    break;
            }
        }

        private object ConvertJsonElementToYamlObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ConvertJsonElementToYamlObject(property.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementToYamlObject(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString() ?? string.Empty;

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    if (element.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    return element.ToString();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.ToString();
            }
        }

        private Dictionary<string, object> ConvertXmlElementToJson(XElement element)
        {
            var result = new Dictionary<string, object>();

            if (element == null)
                return result;

            if (!element.HasElements)
            {
                result[element.Name.LocalName] = element.Value;
                return result;
            }

            var childGroups = element.Elements().GroupBy(e => e.Name.LocalName);

            foreach (var group in childGroups)
            {
                var childElements = group.ToList();

                if (childElements.Count == 1)
                {
                    var child = childElements[0];
                    if (child.HasElements)
                    {
                        result[group.Key] = ConvertXmlElementToJson(child);
                    }
                    else
                    {
                        result[group.Key] = child.Value;
                    }
                }
                else
                {
                    var array = new List<object>();
                    foreach (var child in childElements)
                    {
                        if (child.HasElements)
                        {
                            array.Add(ConvertXmlElementToJson(child));
                        }
                        else
                        {
                            array.Add(child.Value);
                        }
                    }
                    result[group.Key] = array;
                }
            }

            return result;
        }

        private void ConvertObjectToXml(object obj, XElement parent)
        {
            if (obj == null)
                return;

            if (obj is Dictionary<object, object> dict)
            {
                foreach (var kvp in dict)
                {
                    var element = new XElement(kvp.Key.ToString() ?? "Item");
                    ConvertObjectToXml(kvp.Value, element);
                    parent.Add(element);
                }
            }
            else if (obj is IList<object> list)
            {
                foreach (var item in list)
                {
                    var element = new XElement("Item");
                    ConvertObjectToXml(item, element);
                    parent.Add(element);
                }
            }
            else
            {
                parent.Value = obj.ToString() ?? string.Empty;
            }
        }

        #endregion

        #region Secure XML Parsing

        /// <summary>
        /// Parses XML securely to prevent XXE and billion laughs attacks.
        /// </summary>
        private static XDocument ParseXmlSecurely(string xmlContent)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = MaxXmlEntityExpansion,
                MaxCharactersInDocument = MaxUncompressedSize
            };

            using var stringReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            return XDocument.Load(xmlReader);
        }

        #endregion
    }
}
