using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Drawing;
using FileConvert.Core.Interfaces;
using HtmlAgilityPack;
using ICSharpCode.SharpZipLib.Zip;
using Markdig;
using OfficeOpenXml;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RtfPipe;
using VersOne.Epub;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Image = SixLabors.ImageSharp.Image;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using PresentationShape = DocumentFormat.OpenXml.Presentation.Shape;
using DrawingParagraph = DocumentFormat.OpenXml.Drawing.Paragraph;
using QuestPDFFonts = QuestPDF.Helpers.Fonts;
using IOPath = System.IO.Path;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace FileConvert.Infrastructure.Converters
{
    /// <summary>
    /// Handles document format conversions (DOCX, XLSX, PPTX, EPUB, Markdown, HTML).
    /// Uses OpenXML SDK for Office documents, QuestPDF for PDF generation.
    /// </summary>
    public class DocumentConverter : IDocumentConverter
    {
        private static readonly MarkdownPipeline CachedMarkdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        private static readonly Regex MultipleBlankLinesRegex = new(@"\r\n\s*\r\n", RegexOptions.Compiled);
        private static readonly Regex HorizontalWhitespaceRegex = new(@"[ \t]+", RegexOptions.Compiled);

        private const int MaxRowsForXlsxToPdf = 500;
        private const int MaxColumnWidthChars = 50;
        private const int MaxTextContentLength = 1000000;
        private const long MaxUncompressedSize = 1024 * 1024 * 500; // 500MB max
        private const long MaxTotalUncompressedSize = 1024 * 1024 * 1024; // 1GB total
        private const int MaxEntryCount = 10000;

        private const string DocxToHtmlCss = @"
body { font-family: Arial, sans-serif; margin: 40px; line-height: 1.6; }
h1 { font-size: 24px; margin-top: 24px; }
h2 { font-size: 20px; margin-top: 20px; }
h3 { font-size: 16px; margin-top: 16px; }
p { margin: 12px 0; }
table { border-collapse: collapse; width: 100%; margin: 16px 0; }
th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
th { background-color: #f2f2f2; }
ul, ol { margin: 12px 0; padding-left: 24px; }
li { margin: 4px 0; }";

        private readonly IPdfConverter _pdfConverter;

        static DocumentConverter()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public DocumentConverter(IPdfConverter pdfConverter)
        {
            _pdfConverter = pdfConverter;
        }

        #region DOCX Conversions

        public async Task<MemoryStream> ConvertDocxToPdf(MemoryStream docxStream)
        {
            docxStream.Position = 0;

            var textContent = await ExtractTextFromDocxAsync(docxStream);

            if (string.IsNullOrWhiteSpace(textContent))
                throw new ArgumentException("DOCX content is empty or could not be extracted");

            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return outputStream;
        }

        public async Task<MemoryStream> ConvertDocxToHtml(MemoryStream docxStream)
        {
            docxStream.Position = 0;

            var htmlBuilder = new StringBuilder();
            htmlBuilder.AppendLine("<!DOCTYPE html>");
            htmlBuilder.AppendLine("<html>");
            htmlBuilder.AppendLine("<head>");
            htmlBuilder.AppendLine("<meta charset=\"UTF-8\">");
            htmlBuilder.AppendLine("<style>");
            htmlBuilder.AppendLine(DocxToHtmlCss);
            htmlBuilder.AppendLine("</style>");
            htmlBuilder.AppendLine("</head>");
            htmlBuilder.AppendLine("<body>");

            var docxCopy = new MemoryStream(docxStream.ToArray(), true);

            using var wordDoc = WordprocessingDocument.Open(docxCopy, false);
            var mainPart = wordDoc.MainDocumentPart;

            if (mainPart?.Document?.Body != null)
            {
                bool inList = false;
                var elements = mainPart.Document.Body.Elements().ToList();

                for (int i = 0; i < elements.Count; i++)
                {
                    var element = elements[i];
                    var isListItem = element is NumberingInstance ||
                                     element.InnerText?.StartsWith("\u2022") == true ||
                                     element.InnerText?.StartsWith("- ") == true;

                    if (isListItem && !inList)
                    {
                        htmlBuilder.AppendLine("<ul>");
                        inList = true;
                    }
                    else if (!isListItem && inList)
                    {
                        htmlBuilder.AppendLine("</ul>");
                        inList = false;
                    }

                    ProcessDocxElementToHtml(element, htmlBuilder);
                }

                if (inList)
                {
                    htmlBuilder.AppendLine("</ul>");
                }
            }

            htmlBuilder.AppendLine("</body>");
            htmlBuilder.AppendLine("</html>");

            return await WriteStringToStreamAsync(htmlBuilder.ToString());
        }

        private async Task<string> ExtractTextFromDocxAsync(MemoryStream docxStream)
        {
            var textBuilder = new StringBuilder();

            var docxCopy = new MemoryStream(docxStream.ToArray(), true);

            using var wordDoc = WordprocessingDocument.Open(docxCopy, false);
            var mainPart = wordDoc.MainDocumentPart;

            if (mainPart?.Document?.Body != null)
            {
                foreach (var element in mainPart.Document.Body.Elements())
                {
                    var text = ExtractTextFromDocxElement(element);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textBuilder.AppendLine(text);
                    }
                }
            }

            return textBuilder.ToString().Trim();
        }

        private string ExtractTextFromDocxElement(OpenXmlElement element)
        {
            if (element is Paragraph para)
            {
                return para.InnerText;
            }
            else if (element is Table table)
            {
                var tableText = new StringBuilder();
                foreach (var row in table.Elements<TableRow>())
                {
                    var rowTexts = new List<string>();
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        rowTexts.Add(cell.InnerText.Trim());
                    }
                    tableText.AppendLine(string.Join(" | ", rowTexts));
                }
                return tableText.ToString();
            }
            else
            {
                return element.InnerText;
            }
        }

        private void ProcessDocxElementToHtml(OpenXmlElement element, StringBuilder htmlBuilder)
        {
            if (element is Paragraph para)
            {
                var text = para.InnerText;
                if (string.IsNullOrWhiteSpace(text))
                    return;

                var styleId = GetParagraphStyleId(para);
                if (styleId != null)
                {
                    var headingTag = GetHeadingTag(styleId);
                    if (headingTag != null)
                    {
                        htmlBuilder.AppendLine($"<{headingTag}>{EscapeHtml(text)}</{headingTag}>");
                        return;
                    }
                }

                var formattedText = FormatRuns(para);
                htmlBuilder.AppendLine($"<p>{formattedText}</p>");
            }
            else if (element is Table table)
            {
                htmlBuilder.AppendLine("<table>");
                var isFirstRow = true;
                foreach (var row in table.Elements<TableRow>())
                {
                    htmlBuilder.AppendLine("<tr>");
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        var tag = isFirstRow ? "th" : "td";
                        htmlBuilder.AppendLine($"<{tag}>{EscapeHtml(cell.InnerText)}</{tag}>");
                    }
                    htmlBuilder.AppendLine("</tr>");
                    isFirstRow = false;
                }
                htmlBuilder.AppendLine("</table>");
            }
            else if (element is NumberingInstance || element.InnerText?.StartsWith("\u2022") == true ||
                     element.InnerText?.StartsWith("- ") == true)
            {
                htmlBuilder.AppendLine($"<li>{EscapeHtml(element.InnerText)}</li>");
            }
        }

        private static string GetParagraphStyleId(Paragraph para)
        {
            var props = para.ParagraphProperties;
            if (props != null)
            {
                var style = props.ParagraphStyleId;
                if (style != null)
                {
                    return style.Val?.Value;
                }
            }
            return null;
        }

        private static string GetHeadingTag(string styleId)
        {
            if (styleId.Contains("Heading1", StringComparison.OrdinalIgnoreCase)) return "h1";
            if (styleId.Contains("Heading2", StringComparison.OrdinalIgnoreCase)) return "h2";
            if (styleId.Contains("Heading3", StringComparison.OrdinalIgnoreCase)) return "h3";
            return null;
        }

        private static string FormatRuns(Paragraph para)
        {
            var result = new StringBuilder();
            foreach (var run in para.Elements<Run>())
            {
                var text = run.InnerText;
                if (string.IsNullOrEmpty(text))
                    continue;

                var isBold = run.RunProperties?.Bold != null;
                var isItalic = run.RunProperties?.Italic != null;
                var isUnderline = run.RunProperties?.Underline != null;

                var formattedText = EscapeHtml(text);

                if (isUnderline)
                    formattedText = $"<u>{formattedText}</u>";
                if (isItalic)
                    formattedText = $"<em>{formattedText}</em>";
                if (isBold)
                    formattedText = $"<strong>{formattedText}</strong>";

                result.Append(formattedText);
            }
            return result.ToString();
        }

        private static string EscapeHtml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        #endregion

        #region XLSX Conversions

        public async Task<MemoryStream> ConvertXlsxToCsv(MemoryStream xlsxStream)
        {
            using var package = new ExcelPackage(xlsxStream);
            var worksheet = package.Workbook.Worksheets[0];

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                var rowCount = worksheet.Dimension?.Rows ?? 0;
                var colCount = worksheet.Dimension?.Columns ?? 0;

                for (int row = 1; row <= rowCount; row++)
                {
                    var rowValues = new List<string>();
                    for (int col = 1; col <= colCount; col++)
                    {
                        var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                        rowValues.Add(EscapeCsvField(cellValue));
                    }
                    writer.WriteLine(string.Join(",", rowValues));
                }
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertXlsxToJson(MemoryStream xlsxStream)
        {
            using var package = new ExcelPackage(xlsxStream);
            var worksheet = package.Workbook.Worksheets[0];

            var rowCount = worksheet.Dimension?.Rows ?? 0;
            var colCount = worksheet.Dimension?.Columns ?? 0;

            if (rowCount < 2 || colCount == 0)
                return await WriteStringToStreamAsync("[]");

            var headers = new List<string>();
            for (int col = 1; col <= colCount; col++)
            {
                headers.Add(worksheet.Cells[1, col].Value?.ToString() ?? $"Column{col}");
            }

            var rows = new List<Dictionary<string, object>>();
            for (int row = 2; row <= rowCount; row++)
            {
                var rowData = new Dictionary<string, object>();
                for (int col = 1; col <= colCount; col++)
                {
                    var header = headers[col - 1];
                    var cellValue = worksheet.Cells[row, col].Value;

                    if (cellValue == null)
                    {
                        rowData[header] = null;
                    }
                    else if (cellValue is double doubleValue)
                    {
                        if (doubleValue == Math.Truncate(doubleValue))
                        {
                            rowData[header] = (long)doubleValue;
                        }
                        else
                        {
                            rowData[header] = doubleValue;
                        }
                    }
                    else if (cellValue is bool boolValue)
                    {
                        rowData[header] = boolValue;
                    }
                    else if (cellValue is DateTime dateTimeValue)
                    {
                        rowData[header] = dateTimeValue.ToString("o");
                    }
                    else
                    {
                        rowData[header] = cellValue.ToString();
                    }
                }
                rows.Add(rowData);
            }

            return await WriteStringToStreamAsync(System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }

        public async Task<MemoryStream> ConvertXlsxToPdf(MemoryStream xlsxStream)
        {
            xlsxStream.Position = 0;

            using var package = new ExcelPackage(xlsxStream);
            var worksheet = package.Workbook.Worksheets[0];

            var originalRowCount = worksheet.Dimension?.Rows ?? 0;
            var colCount = worksheet.Dimension?.Columns ?? 0;

            if (originalRowCount == 0 || colCount == 0)
                throw new ArgumentException("XLSX spreadsheet is empty");

            var rowCount = Math.Min(originalRowCount, MaxRowsForXlsxToPdf);
            var wasTruncated = originalRowCount > MaxRowsForXlsxToPdf;

            var tableData = new List<List<string>>();
            var columnWidths = new int[colCount];

            for (int row = 1; row <= rowCount; row++)
            {
                var rowData = new List<string>();
                for (int col = 1; col <= colCount; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Text ?? string.Empty;
                    rowData.Add(cellValue);

                    if (cellValue.Length > columnWidths[col - 1])
                    {
                        columnWidths[col - 1] = Math.Min(cellValue.Length, MaxColumnWidthChars);
                    }
                }
                tableData.Add(rowData);
            }

            var outputStream = new MemoryStream();

            var textBuilder = new StringBuilder();

            if (wasTruncated)
            {
                textBuilder.AppendLine($"WARNING: Document truncated to {MaxRowsForXlsxToPdf} of {originalRowCount} rows");
                textBuilder.AppendLine(new string('-', 80));
                textBuilder.AppendLine();
            }

            for (int row = 0; row < tableData.Count; row++)
            {
                var rowData = tableData[row];
                var line = string.Join(" | ", rowData.Select((cell, col) =>
                    cell.PadRight(columnWidths[col])));
                textBuilder.AppendLine(line);
                textBuilder.AppendLine(new string('-', line.Length > 80 ? 80 : line.Length));
            }

            var textContent = textBuilder.ToString();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(0.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(7).FontFamily(QuestPDFFonts.CourierNew));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return await Task.FromResult(outputStream);
        }

        #endregion

        #region PPTX Conversions

        public async Task<MemoryStream> ConvertPptxToPdf(MemoryStream pptxStream)
        {
            pptxStream.Position = 0;

            var slideTexts = new List<string>();

            var pptxCopy = new MemoryStream(pptxStream.ToArray(), true);

            using var presentation = PresentationDocument.Open(pptxCopy, false);
            var presentationPart = presentation.PresentationPart;

            if (presentationPart == null)
                throw new ArgumentException("PPTX file has no presentation part");

            var slideParts = presentationPart.SlideParts;
            if (slideParts == null || !slideParts.Any())
                throw new ArgumentException("PPTX presentation contains no slides");

            foreach (var slidePart in slideParts)
            {
                if (slidePart?.Slide?.CommonSlideData?.ShapeTree != null)
                {
                    var slideText = ExtractTextFromSlide(slidePart.Slide.CommonSlideData.ShapeTree);
                    if (!string.IsNullOrWhiteSpace(slideText))
                    {
                        slideTexts.Add(slideText);
                    }
                }
            }

            if (slideTexts.Count == 0)
                throw new ArgumentException("PPTX presentation contains no extractable text content");

            var textBuilder = new StringBuilder();
            for (int i = 0; i < slideTexts.Count; i++)
            {
                textBuilder.AppendLine($"=== Slide {i + 1} ===");
                textBuilder.AppendLine(slideTexts[i]);
                textBuilder.AppendLine();
            }

            var textContent = textBuilder.ToString().Trim();

            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertPptxToPng(MemoryStream pptxStream)
        {
            // Use PDF converter for PPTX to image conversion
            var pdfStream = await ConvertPptxToPdf(pptxStream);
            return await _pdfConverter.ConvertPdfToPng(pdfStream);
        }

        public async Task<MemoryStream> ConvertPptxToJpg(MemoryStream pptxStream)
        {
            // Use PDF converter for PPTX to image conversion
            var pdfStream = await ConvertPptxToPdf(pptxStream);
            return await _pdfConverter.ConvertPdfToJpg(pdfStream);
        }

        private string ExtractTextFromSlide(ShapeTree shapeTree)
        {
            var textBuilder = new StringBuilder();

            foreach (var shape in shapeTree.Elements<PresentationShape>())
            {
                if (textBuilder.Length >= MaxTextContentLength)
                    break;

                var textBody = shape.TextBody;
                if (textBody != null)
                {
                    foreach (var paragraph in textBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>())
                    {
                        if (textBuilder.Length >= MaxTextContentLength)
                            break;

                        var paragraphText = new StringBuilder();
                        foreach (var run in paragraph.Elements<DocumentFormat.OpenXml.Drawing.Run>())
                        {
                            if (run.Text != null)
                            {
                                paragraphText.Append(run.Text.Text);
                            }
                        }

                        if (paragraphText.Length > 0)
                        {
                            textBuilder.AppendLine(paragraphText.ToString());
                        }
                    }
                }
            }

            return textBuilder.ToString().Trim();
        }

        #endregion

        #region Markdown Conversions

        public async Task<MemoryStream> ConvertMarkdownToHtml(MemoryStream markdownStream)
        {
            var markdownContent = Encoding.UTF8.GetString(markdownStream.ToArray());
            var htmlContent = Markdown.ToHtml(markdownContent, CachedMarkdownPipeline);

            var outputStream = new MemoryStream();
            using (var writer = new StreamWriter(outputStream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(htmlContent);
            }
            outputStream.Position = 0;

            return await Task.FromResult(outputStream);
        }

        public async Task<MemoryStream> ConvertMarkdownToPdf(MemoryStream markdownStream)
        {
            markdownStream.Position = 0;
            var markdownContent = Encoding.UTF8.GetString(markdownStream.ToArray());

            if (string.IsNullOrWhiteSpace(markdownContent))
                throw new ArgumentException("Markdown content is empty");

            var htmlContent = Markdown.ToHtml(markdownContent, CachedMarkdownPipeline);

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
            if (scriptNodes != null)
            {
                foreach (var node in scriptNodes)
                {
                    node.Remove();
                }
            }

            var textContent = ExtractTextFromHtmlNode(doc.DocumentNode);

            textContent = MultipleBlankLinesRegex.Replace(textContent, "\n\n");
            textContent = HorizontalWhitespaceRegex.Replace(textContent, " ");
            textContent = textContent.Trim();

            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return outputStream;
        }

        #endregion

        #region EPUB Conversions

        public async Task<MemoryStream> ConvertEpubToPdf(MemoryStream epubStream)
        {
            epubStream.Position = 0;

            var epubBytes = epubStream.ToArray();
            var tempFilePath = IOPath.Combine(IOPath.GetTempPath(), $"epub_{Guid.NewGuid()}.epub");

            try
            {
                await File.WriteAllBytesAsync(tempFilePath, epubBytes);
                var book = await EpubReader.ReadBookAsync(tempFilePath);

                var textBuilder = new StringBuilder();

                foreach (var chapterFile in book.ReadingOrder)
                {
                    var chapterContent = chapterFile.Content;

                    var doc = new HtmlDocument();
                    doc.LoadHtml(chapterContent);

                    var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
                    if (scriptNodes != null)
                    {
                        foreach (var node in scriptNodes)
                        {
                            node.Remove();
                        }
                    }

                    var chapterText = ExtractTextFromHtmlNode(doc.DocumentNode);

                    chapterText = MultipleBlankLinesRegex.Replace(chapterText, "\n\n");
                    chapterText = HorizontalWhitespaceRegex.Replace(chapterText, " ");
                    chapterText = chapterText.Trim();

                    if (!string.IsNullOrWhiteSpace(chapterText))
                    {
                        textBuilder.AppendLine(chapterText);
                        textBuilder.AppendLine();
                        textBuilder.AppendLine();
                    }
                }

                var fullText = textBuilder.ToString().Trim();

                if (string.IsNullOrWhiteSpace(fullText))
                    throw new ArgumentException("EPUB content is empty or could not be extracted");

                var outputStream = new MemoryStream();

                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        page.Content().Text(fullText);
                    });
                }).GeneratePdf(outputStream);

                outputStream.Position = 0;
                return outputStream;
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        public async Task<MemoryStream> ConvertEpubToTxt(MemoryStream epubStream)
        {
            epubStream.Position = 0;

            var epubBytes = epubStream.ToArray();
            var tempFilePath = IOPath.Combine(IOPath.GetTempPath(), $"epub_{Guid.NewGuid()}.epub");

            try
            {
                await File.WriteAllBytesAsync(tempFilePath, epubBytes);
                var book = await EpubReader.ReadBookAsync(tempFilePath);

                var textBuilder = new StringBuilder();

                foreach (var chapterFile in book.ReadingOrder)
                {
                    var chapterContent = chapterFile.Content;

                    var doc = new HtmlDocument();
                    doc.LoadHtml(chapterContent);

                    var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
                    if (scriptNodes != null)
                    {
                        foreach (var node in scriptNodes)
                        {
                            node.Remove();
                        }
                    }

                    var chapterText = ExtractTextFromHtmlNode(doc.DocumentNode);

                    chapterText = MultipleBlankLinesRegex.Replace(chapterText, "\n\n");
                    chapterText = HorizontalWhitespaceRegex.Replace(chapterText, " ");
                    chapterText = chapterText.Trim();

                    if (!string.IsNullOrWhiteSpace(chapterText))
                    {
                        textBuilder.AppendLine(chapterText);
                        textBuilder.AppendLine();
                        textBuilder.AppendLine();
                    }
                }

                var fullText = textBuilder.ToString().Trim();

                if (string.IsNullOrWhiteSpace(fullText))
                    throw new ArgumentException("EPUB content is empty or could not be extracted");

                return await WriteStringToStreamAsync(fullText);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        #endregion

        #region HTML Conversions

        public async Task<MemoryStream> ConvertHtmlToText(MemoryStream htmlStream)
        {
            var htmlContent = Encoding.UTF8.GetString(htmlStream.ToArray());

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
            if (scriptNodes != null)
            {
                foreach (var node in scriptNodes)
                {
                    node.Remove();
                }
            }

            var textContent = ExtractTextFromHtmlNode(doc.DocumentNode);

            textContent = MultipleBlankLinesRegex.Replace(textContent, "\n\n");
            textContent = HorizontalWhitespaceRegex.Replace(textContent, " ");
            textContent = textContent.Trim();

            return await WriteStringToStreamAsync(textContent);
        }

        public async Task<MemoryStream> ConvertHtmlToPdf(MemoryStream htmlStream)
        {
            htmlStream.Position = 0;
            var htmlContent = Encoding.UTF8.GetString(htmlStream.ToArray());

            if (string.IsNullOrWhiteSpace(htmlContent))
                throw new ArgumentException("HTML content is empty");

            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            var scriptNodes = doc.DocumentNode.SelectNodes("//script|//style");
            if (scriptNodes != null)
            {
                foreach (var node in scriptNodes)
                {
                    node.Remove();
                }
            }

            var textContent = ExtractFormattedTextFromHtml(doc.DocumentNode);

            textContent = MultipleBlankLinesRegex.Replace(textContent, "\n\n");
            textContent = HorizontalWhitespaceRegex.Replace(textContent, " ");
            textContent = textContent.Trim();

            if (string.IsNullOrWhiteSpace(textContent))
                throw new ArgumentException("HTML content contains no extractable text");

            var outputStream = new MemoryStream();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Text(textContent);
                });
            }).GeneratePdf(outputStream);

            outputStream.Position = 0;
            return await Task.FromResult(outputStream);
        }

        private string ExtractTextFromHtmlNode(HtmlNode node)
        {
            var blockElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6", "li", "tr"
            };

            if (node.NodeType == HtmlNodeType.Text)
                return node.InnerText;

            if (node.NodeType == HtmlNodeType.Comment)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var child in node.ChildNodes)
            {
                sb.Append(ExtractTextFromHtmlNode(child));
            }

            if (blockElements.Contains(node.Name))
                sb.Append('\n');

            return sb.ToString();
        }

        private string ExtractFormattedTextFromHtml(HtmlNode node)
        {
            var blockElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6", "li", "tr"
            };

            if (node.NodeType == HtmlNodeType.Text)
                return node.InnerText;

            if (node.NodeType == HtmlNodeType.Comment)
                return string.Empty;

            var sb = new StringBuilder();
            var tagName = node.Name.ToLowerInvariant();

            switch (tagName)
            {
                case "h1":
                    sb.AppendLine();
                    sb.AppendLine("=".PadRight(60, '='));
                    break;
                case "h2":
                    sb.AppendLine();
                    sb.AppendLine(new string('-', 40));
                    break;
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    sb.AppendLine();
                    sb.Append("### ");
                    break;
            }

            foreach (var child in node.ChildNodes)
            {
                var childText = ExtractFormattedTextFromHtml(child);

                if (child.NodeType == HtmlNodeType.Element)
                {
                    var childTagName = child.Name.ToLowerInvariant();
                    switch (childTagName)
                    {
                        case "strong":
                        case "b":
                            childText = $"**{childText.Trim()}** ";
                            break;
                        case "em":
                        case "i":
                            childText = $"_{childText.Trim()}_ ";
                            break;
                        case "a":
                            var href = child.GetAttributeValue("href", "");
                            if (!string.IsNullOrEmpty(href))
                            {
                                childText = $"{childText.Trim()} [{href}] ";
                            }
                            break;
                        case "li":
                            childText = $"  - {childText.Trim()}";
                            break;
                    }
                }

                sb.Append(childText);
            }

            if (blockElements.Contains(tagName) || tagName.StartsWith("h"))
            {
                sb.AppendLine();
            }

            if (tagName == "ul" || tagName == "ol")
            {
                sb.AppendLine();
            }

            if (tagName == "tr")
            {
                sb.AppendLine();
            }
            else if (tagName == "td" || tagName == "th")
            {
                sb.Append(" | ");
            }

            return sb.ToString();
        }

        #endregion

        #region RTF Conversions

        public async Task<MemoryStream> ConvertRtfToHtml(MemoryStream rtfStream)
        {
            if (rtfStream.Length > MaxUncompressedSize)
                throw new ArgumentException($"RTF file exceeds maximum size limit of {MaxUncompressedSize} bytes");

            rtfStream.Position = 0;

            using var reader = new StreamReader(rtfStream, Encoding.UTF8, leaveOpen: true);
            var rtfContent = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rtfContent))
                throw new ArgumentException("RTF content is empty");

            var htmlContent = Rtf.ToHtml(rtfContent);

            var fullHtml = new StringBuilder();
            if (!htmlContent.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            {
                fullHtml.AppendLine("<!DOCTYPE html>");
                fullHtml.AppendLine("<html>");
                fullHtml.AppendLine("<head>");
                fullHtml.AppendLine("<meta charset=\"UTF-8\">");
                fullHtml.AppendLine("<style>");
                fullHtml.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
                fullHtml.AppendLine("</style>");
                fullHtml.AppendLine("</head>");
                fullHtml.AppendLine("<body>");
            }

            fullHtml.Append(htmlContent);

            if (!htmlContent.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            {
                fullHtml.AppendLine();
                fullHtml.AppendLine("</body>");
                fullHtml.AppendLine("</html>");
            }

            return await WriteStringToStreamAsync(fullHtml.ToString());
        }

        public async Task<MemoryStream> ConvertRtfToTxt(MemoryStream rtfStream)
        {
            if (rtfStream.Length > MaxUncompressedSize)
                throw new ArgumentException($"RTF file exceeds maximum size limit of {MaxUncompressedSize} bytes");

            rtfStream.Position = 0;

            using var reader = new StreamReader(rtfStream, Encoding.UTF8, leaveOpen: true);
            var rtfContent = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rtfContent))
                throw new ArgumentException("RTF content is empty");

            var htmlContent = Rtf.ToHtml(rtfContent);
            var plainText = StripHtmlTags(htmlContent);

            return await WriteStringToStreamAsync(plainText);
        }

        private static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var text = doc.DocumentNode.InnerText;

            text = MultipleBlankLinesRegex.Replace(text, "\n\n");
            text = HorizontalWhitespaceRegex.Replace(text, " ");
            text = text.Replace("&nbsp;", " ")
                       .Replace("&amp;", "&")
                       .Replace("&lt;", "<")
                       .Replace("&gt;", ">")
                       .Replace("&quot;", "\"");

            return text.Trim();
        }

        #endregion

        #region OpenDocument Conversions (ODT/ODS)

        public async Task<MemoryStream> ConvertOdtToDocx(MemoryStream odtStream)
        {
            if (odtStream.Length > MaxUncompressedSize)
                throw new ArgumentException($"ODT file exceeds maximum size limit of {MaxUncompressedSize} bytes");

            odtStream.Position = 0;

            var textContent = await ExtractTextFromOdtAsync(odtStream);

            if (string.IsNullOrWhiteSpace(textContent))
                throw new ArgumentException("ODT content is empty or could not be extracted");

            var outputStream = new MemoryStream();

            using (var wordDoc = WordprocessingDocument.Create(outputStream, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new WordDocument();
                var body = mainPart.Document.AppendChild(new Body());

                var paragraphs = textContent.Split('\n');
                foreach (var paraText in paragraphs)
                {
                    if (!string.IsNullOrWhiteSpace(paraText))
                    {
                        var para = body.AppendChild(new Paragraph());
                        para.AppendChild(new Run(new WordText(paraText.Trim())));
                    }
                    else
                    {
                        body.AppendChild(new Paragraph());
                    }
                }

                mainPart.Document.Save();
            }

            outputStream.Position = 0;
            return outputStream;
        }

        private async Task<string> ExtractTextFromOdtAsync(MemoryStream odtStream)
        {
            var textBuilder = new StringBuilder();
            long totalExtractedSize = 0;
            int entryCount = 0;

            odtStream.Position = 0;
            using var zipArchive = new ZipInputStream(odtStream);
            zipArchive.IsStreamOwner = false;

            ZipEntry entry;
            while ((entry = zipArchive.GetNextEntry()) != null)
            {
                entryCount++;
                if (entryCount > MaxEntryCount)
                    throw new ArgumentException($"ODT archive contains too many entries (max {MaxEntryCount})");

                if (entry.Size > MaxUncompressedSize)
                    throw new ArgumentException($"ODT archive entry '{entry.Name}' exceeds maximum size limit");

                if (!entry.IsDirectory && entry.Name.EndsWith("content.xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(zipArchive, Encoding.UTF8, false, -1, true);
                    var contentXml = await reader.ReadToEndAsync();

                    totalExtractedSize += contentXml.Length;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new ArgumentException("ODT archive total uncompressed size exceeds limit");

                    var doc = ParseXmlSecurely(contentXml);

                    XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

                    var textElements = doc.Descendants()
                        .Where(e => e.Name == textNs + "p" || e.Name == textNs + "h");

                    foreach (var element in textElements)
                    {
                        if (textBuilder.Length >= MaxTextContentLength)
                            break;

                        var text = element.Value;
                        if (!string.IsNullOrWhiteSpace(text))
                            textBuilder.AppendLine(text);
                    }

                    break;
                }
            }

            return textBuilder.ToString().Trim();
        }

        public async Task<MemoryStream> ConvertOdsToXlsx(MemoryStream odsStream)
        {
            if (odsStream.Length > MaxUncompressedSize)
                throw new ArgumentException($"ODS file exceeds maximum size limit of {MaxUncompressedSize} bytes");

            odsStream.Position = 0;

            var tableData = await ExtractDataFromOdsAsync(odsStream);

            var outputStream = new MemoryStream();

            using (var package = new ExcelPackage(outputStream))
            {
                var worksheet = package.Workbook.Worksheets.Add("Sheet1");

                for (int row = 0; row < tableData.Count; row++)
                {
                    for (int col = 0; col < tableData[row].Count; col++)
                    {
                        worksheet.Cells[row + 1, col + 1].Value = tableData[row][col];
                    }
                }

                package.Save();
            }

            outputStream.Position = 0;
            return outputStream;
        }

        private async Task<List<List<string>>> ExtractDataFromOdsAsync(MemoryStream odsStream)
        {
            var tableData = new List<List<string>>();
            long totalExtractedSize = 0;
            int entryCount = 0;

            odsStream.Position = 0;
            using var zipArchive = new ZipInputStream(odsStream);
            zipArchive.IsStreamOwner = false;

            ZipEntry entry;
            while ((entry = zipArchive.GetNextEntry()) != null)
            {
                entryCount++;
                if (entryCount > MaxEntryCount)
                    throw new ArgumentException($"ODS archive contains too many entries (max {MaxEntryCount})");

                if (entry.Size > MaxUncompressedSize)
                    throw new ArgumentException($"ODS archive entry '{entry.Name}' exceeds maximum size limit");

                if (!entry.IsDirectory && entry.Name.EndsWith("content.xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(zipArchive, Encoding.UTF8, false, -1, true);
                    var contentXml = await reader.ReadToEndAsync();

                    totalExtractedSize += contentXml.Length;
                    if (totalExtractedSize > MaxTotalUncompressedSize)
                        throw new ArgumentException("ODS archive total uncompressed size exceeds limit");

                    var doc = ParseXmlSecurely(contentXml);

                    XNamespace tableNs = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
                    XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

                    var rows = doc.Descendants(tableNs + "table-row");

                    foreach (var row in rows)
                    {
                        if (tableData.Count >= MaxEntryCount)
                            break;

                        var rowData = new List<string>();
                        var cells = row.Elements(tableNs + "table-cell");

                        foreach (var cell in cells)
                        {
                            var textElement = cell.Element(textNs + "p");
                            var cellValue = textElement?.Value ?? string.Empty;
                            rowData.Add(cellValue);
                        }

                        if (rowData.Count > 0)
                            tableData.Add(rowData);
                    }

                    break;
                }
            }

            if (tableData.Count == 0)
                tableData.Add(new List<string> { string.Empty });

            return tableData;
        }

        private static XDocument ParseXmlSecurely(string xmlContent)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 1024,
                MaxCharactersInDocument = MaxTextContentLength * 2
            };

            using var stringReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            return XDocument.Load(xmlReader);
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

        #endregion
    }
}
