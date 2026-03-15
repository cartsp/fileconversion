using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileConvert.Core;
using FileConvert.Core.Entities;
using FileConvert.Core.Interfaces;
using FileConvert.Core.ValueObjects;
using FileConvert.Infrastructure.Converters;
using OfficeOpenXml;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace FileConvert.Infrastructure
{
    /// <summary>
    /// Main file conversion service that delegates to specialized converters.
    /// This service acts as a facade, routing conversion requests to the appropriate converter.
    /// </summary>
    public class FileConversionService : IFileConvertors
    {
        private static IImmutableList<ConvertorDetails> Convertors = ImmutableList<ConvertorDetails>.Empty;

        private readonly IImageConverter _imageConverter;
        private readonly ITiffConverter _tiffConverter;
        private readonly ISvgConverter _svgConverter;
        private readonly IModernImageConverter _modernImageConverter;
        private readonly IDataConverter _dataConverter;
        private readonly IDocumentConverter _documentConverter;
        private readonly IPdfConverter _pdfConverter;
        private readonly IArchiveConverter _archiveConverter;
        private readonly ISpecialConverter _specialConverter;

        static FileConversionService()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public FileConversionService()
            : this(
                new ImageConverter(),
                new TiffConverter(),
                new SvgConverter(),
                new ModernImageConverter(),
                new DataConverter(),
                new DocumentConverter(new PdfConverter()),
                new PdfConverter(),
                new ArchiveConverter(),
                new SpecialConverter())
        {
        }

        public FileConversionService(
            IImageConverter imageConverter,
            ITiffConverter tiffConverter,
            ISvgConverter svgConverter,
            IModernImageConverter modernImageConverter,
            IDataConverter dataConverter,
            IDocumentConverter documentConverter,
            IPdfConverter pdfConverter,
            IArchiveConverter archiveConverter,
            ISpecialConverter specialConverter)
        {
            _imageConverter = imageConverter;
            _tiffConverter = tiffConverter;
            _svgConverter = svgConverter;
            _modernImageConverter = modernImageConverter;
            _dataConverter = dataConverter;
            _documentConverter = documentConverter;
            _pdfConverter = pdfConverter;
            _archiveConverter = archiveConverter;
            _specialConverter = specialConverter;

            CreateConvertorList();
        }

        public void CreateConvertorList()
        {
            var convertorListBuilder = ImmutableList.CreateBuilder<ConvertorDetails>();

            // CSV conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.xlsx, ConvertCSVToExcel));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.json, ConvertCSVToJSON));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.yaml, ConvertCSVToYAML));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.csv, FileExtension.yml, ConvertCSVToYAML));

            // XLSX conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.xlsx, FileExtension.csv, ConvertXLSXToCSV));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.xlsx, FileExtension.json, ConvertXLSXToJSON));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.xlsx, FileExtension.pdf, ConvertXlsxToPdf));

            // DOCX conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.docx, FileExtension.pdf, ConvertDocxToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.docx, FileExtension.html, ConvertDocxToHtml));

            // PPTX conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.pdf, ConvertPptxToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.png, ConvertPptxToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.jpg, ConvertPptxToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pptx, FileExtension.jpeg, ConvertPptxToJpg));

            // HTML conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.html, FileExtension.txt, ConvertHTMLToText));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.html, FileExtension.pdf, ConvertHtmlToPdf));

            // RTF conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.rtf, FileExtension.html, ConvertRtfToHtml));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.rtf, FileExtension.txt, ConvertRtfToTxt));

            // OpenDocument conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.odt, FileExtension.docx, ConvertOdtToDocx));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.ods, FileExtension.xlsx, ConvertOdsToXlsx));

            // Image conversions - to JPG
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.jpg, ConvertImageTojpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.jpeg, ConvertImageTojpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.jpeg, ConvertImageTojpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.jpg, ConvertImageTojpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.jpeg, ConvertImageTojpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.jpg, ConvertImageTojpg));

            // Image conversions - to PNG
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.png, ConvertImageToPNG));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.png, ConvertImageToPNG));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.png, ConvertImageToPNG));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jfif, FileExtension.png, ConvertImageToPNG));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.png, ConvertImageToPNG));

            // Image conversions - to GIF
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.gif, ConvertImageToGIF));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.gif, ConvertImageToGIF));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.gif, ConvertImageToGIF));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jfif, FileExtension.gif, ConvertImageToGIF));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.gif, ConvertImageToGIF));

            // Image conversions - to WebP
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.webp, ConvertImageToWebP));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.webp, ConvertImageToWebP));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.webp, ConvertImageToWebP));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.webp, ConvertImageToWebP));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.webp, ConvertImageToWebP));

            // WebP conversions - from WebP
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.jpg, ConvertWebPToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.jpeg, ConvertWebPToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.png, ConvertWebPToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.gif, ConvertWebPToGif));

            // TIFF conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.png, ConvertTiffToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.jpg, ConvertTiffToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.jpeg, ConvertTiffToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tif, FileExtension.webp, ConvertTiffToWebP));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.png, ConvertTiffToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.jpg, ConvertTiffToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.jpeg, ConvertTiffToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tiff, FileExtension.webp, ConvertTiffToWebP));

            // JSON conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.xml, ConvertJSONToXML));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.csv, ConvertJSONToCSV));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.yaml, ConvertJSONToYAML));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.json, FileExtension.yml, ConvertJSONToYAML));

            // XML conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.json, ConvertXMLToJSON));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.csv, ConvertXMLToCSV));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.yaml, ConvertXMLToYAML));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.xml, FileExtension.yml, ConvertXMLToYAML));

            // YAML conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.yaml, FileExtension.json, ConvertYAMLToJSON));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.yml, FileExtension.json, ConvertYAMLToJSON));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.yaml, FileExtension.xml, ConvertYAMLToXML));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.yml, FileExtension.xml, ConvertYAMLToXML));

            // TSV conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tsv, FileExtension.csv, ConvertTSVToCSV));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tsv, FileExtension.json, ConvertTSVToJSON));

            // Markdown conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.md, FileExtension.html, ConvertMarkdownToHTML));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.md, FileExtension.pdf, ConvertMarkdownToPdf));

            // EPUB conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.epub, FileExtension.pdf, ConvertEpubToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.epub, FileExtension.txt, ConvertEpubToTxt));

            // ICO conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.ico, ConvertImageToIco));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.ico, ConvertImageToIco));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.ico, ConvertImageToIco));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.ico, ConvertImageToIco));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.ico, ConvertImageToIco));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.ico, ConvertImageToIco));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.ico, FileExtension.png, ConvertIcoToPng));

            // SVG conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.png, ConvertSvgToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.jpg, ConvertSvgToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.jpeg, ConvertSvgToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.svg, FileExtension.webp, ConvertSvgToWebP));

            // Archive conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.gz, FileExtension.tar, ConvertGzToTar));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tgz, FileExtension.tar, ConvertGzToTar));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tar, FileExtension.gz, ConvertTarToGz));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tar, FileExtension.tgz, ConvertTarToGz));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bz2, FileExtension.tar, ConvertBz2ToTar));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tbz2, FileExtension.tar, ConvertBz2ToTar));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.zip, FileExtension.tar, ConvertZipToTar));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.tar, FileExtension.zip, ConvertTarToZip));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension._7z, FileExtension.zip, Convert7zToZip));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension._7z, FileExtension.tar, Convert7zToTar));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.rar, FileExtension.zip, ConvertRarToZip));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.rar, FileExtension.tar, ConvertRarToTar));

            // JPEG 2000 conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.png, ConvertJp2ToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.jpg, ConvertJp2ToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.jpeg, ConvertJp2ToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jp2, FileExtension.webp, ConvertJp2ToWebP));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.png, ConvertJp2ToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.jpg, ConvertJp2ToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.jpeg, ConvertJp2ToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.j2k, FileExtension.webp, ConvertJp2ToWebP));

            // Image to PDF conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.png, FileExtension.pdf, ConvertImageToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpg, FileExtension.pdf, ConvertImageToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jpeg, FileExtension.pdf, ConvertImageToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.gif, FileExtension.pdf, ConvertImageToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.bmp, FileExtension.pdf, ConvertImageToPdf));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.webp, FileExtension.pdf, ConvertImageToPdf));

            // PDF conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.png, ConvertPdfToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.jpg, ConvertPdfToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.jpeg, ConvertPdfToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.pdf, FileExtension.txt, ConvertPdfToText));

            // HEIC/HEIF conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.jpg, ConvertHeicToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.jpeg, ConvertHeicToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.png, ConvertHeicToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heic, FileExtension.webp, ConvertHeicToWebp));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.jpg, ConvertHeicToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.jpeg, ConvertHeicToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.png, ConvertHeicToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.heif, FileExtension.webp, ConvertHeicToWebp));

            // AVIF conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.jpg, ConvertAvifToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.jpeg, ConvertAvifToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.png, ConvertAvifToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.avif, FileExtension.webp, ConvertAvifToWebp));

            // JPEG XL conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.jpg, ConvertJxlToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.jpeg, ConvertJxlToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.png, ConvertJxlToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.jxl, FileExtension.webp, ConvertJxlToWebp));

            // DNG conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.jpg, ConvertDngToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.jpeg, ConvertDngToJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.png, ConvertDngToPng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.dng, FileExtension.webp, ConvertDngToWebp));

            // QR/Barcode conversions
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.txt, FileExtension.png, ConvertTextToQrCodePng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.qr, FileExtension.png, ConvertTextToQrCodePng));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.txt, FileExtension.jpg, ConvertTextToBarcodeJpg));
            convertorListBuilder.Add(new ConvertorDetails(FileExtension.txt, FileExtension.jpeg, ConvertTextToBarcodeJpg));

            Convertors = convertorListBuilder.ToImmutable();
        }

        public IImmutableList<ConvertorDetails> GetConvertorsForFile(string inputFileName)
        {
            return Convertors.Where(cd => cd.ExtensionToConvert == Path.GetExtension(inputFileName)).ToImmutableList();
        }

        public IImmutableList<ConvertorDetails> GetAllAvailableConvertors()
        {
            return Convertors;
        }

        #region Image Conversion Delegates

        public Task<MemoryStream> ConvertImageTojpg(MemoryStream imageStream) => _imageConverter.ConvertToJpg(imageStream);
        public Task<MemoryStream> ConvertImageToPNG(MemoryStream imageStream) => _imageConverter.ConvertToPng(imageStream);
        public Task<MemoryStream> ConvertImageToGIF(MemoryStream imageStream) => _imageConverter.ConvertToGif(imageStream);
        public Task<MemoryStream> ConvertImageToWebP(MemoryStream imageStream) => _imageConverter.ConvertToWebP(imageStream);
        public Task<MemoryStream> ConvertWebPToJpg(MemoryStream webPStream) => _imageConverter.ConvertWebPToJpg(webPStream);
        public Task<MemoryStream> ConvertWebPToPng(MemoryStream webPStream) => _imageConverter.ConvertWebPToPng(webPStream);
        public Task<MemoryStream> ConvertWebPToGif(MemoryStream webPStream) => _imageConverter.ConvertWebPToGif(webPStream);
        public Task<MemoryStream> ConvertImageToIco(MemoryStream imageStream) => _imageConverter.ConvertImageToIco(imageStream);
        public Task<MemoryStream> ConvertIcoToPng(MemoryStream icoStream) => _imageConverter.ConvertIcoToPng(icoStream);

        #endregion

        #region TIFF Conversion Delegates

        public Task<MemoryStream> ConvertTiffToPng(MemoryStream tiffStream) => _tiffConverter.ConvertToPng(tiffStream);
        public Task<MemoryStream> ConvertTiffToJpg(MemoryStream tiffStream) => _tiffConverter.ConvertToJpg(tiffStream);
        public Task<MemoryStream> ConvertTiffToWebP(MemoryStream tiffStream) => _tiffConverter.ConvertToWebP(tiffStream);

        #endregion

        #region SVG Conversion Delegates

        public Task<MemoryStream> ConvertSvgToPng(MemoryStream svgStream) => _svgConverter.ConvertToPng(svgStream);
        public Task<MemoryStream> ConvertSvgToJpg(MemoryStream svgStream) => _svgConverter.ConvertToJpg(svgStream);
        public Task<MemoryStream> ConvertSvgToWebP(MemoryStream svgStream) => _svgConverter.ConvertToWebP(svgStream);

        #endregion

        #region Modern Image Format Delegates

        public Task<MemoryStream> ConvertHeicToJpg(MemoryStream heicStream) => _modernImageConverter.ConvertHeicToJpg(heicStream);
        public Task<MemoryStream> ConvertHeicToPng(MemoryStream heicStream) => _modernImageConverter.ConvertHeicToPng(heicStream);
        public Task<MemoryStream> ConvertHeicToWebp(MemoryStream heicStream) => _modernImageConverter.ConvertHeicToWebP(heicStream);
        public Task<MemoryStream> ConvertAvifToJpg(MemoryStream avifStream) => _modernImageConverter.ConvertAvifToJpg(avifStream);
        public Task<MemoryStream> ConvertAvifToPng(MemoryStream avifStream) => _modernImageConverter.ConvertAvifToPng(avifStream);
        public Task<MemoryStream> ConvertAvifToWebp(MemoryStream avifStream) => _modernImageConverter.ConvertAvifToWebP(avifStream);
        public Task<MemoryStream> ConvertJxlToJpg(MemoryStream jxlStream) => _modernImageConverter.ConvertJxlToJpg(jxlStream);
        public Task<MemoryStream> ConvertJxlToPng(MemoryStream jxlStream) => _modernImageConverter.ConvertJxlToPng(jxlStream);
        public Task<MemoryStream> ConvertJxlToWebp(MemoryStream jxlStream) => _modernImageConverter.ConvertJxlToWebP(jxlStream);
        public Task<MemoryStream> ConvertDngToJpg(MemoryStream dngStream) => _modernImageConverter.ConvertDngToJpg(dngStream);
        public Task<MemoryStream> ConvertDngToPng(MemoryStream dngStream) => _modernImageConverter.ConvertDngToPng(dngStream);
        public Task<MemoryStream> ConvertDngToWebp(MemoryStream dngStream) => _modernImageConverter.ConvertDngToWebP(dngStream);
        public Task<MemoryStream> ConvertJp2ToPng(MemoryStream jp2Stream) => _modernImageConverter.ConvertJp2ToPng(jp2Stream);
        public Task<MemoryStream> ConvertJp2ToJpg(MemoryStream jp2Stream) => _modernImageConverter.ConvertJp2ToJpg(jp2Stream);
        public Task<MemoryStream> ConvertJp2ToWebP(MemoryStream jp2Stream) => _modernImageConverter.ConvertJp2ToWebP(jp2Stream);

        #endregion

        #region Data Format Delegates

        public Task<MemoryStream> ConvertCSVToExcel(MemoryStream csvStream) => _dataConverter.ConvertCsvToXlsx(csvStream);
        public Task<MemoryStream> ConvertCSVToJSON(MemoryStream csvStream) => _dataConverter.ConvertCsvToJson(csvStream);
        public Task<MemoryStream> ConvertCSVToYAML(MemoryStream csvStream) => _dataConverter.ConvertCsvToYaml(csvStream);
        public Task<MemoryStream> ConvertJSONToXML(MemoryStream jsonStream) => _dataConverter.ConvertJsonToXml(jsonStream);
        public Task<MemoryStream> ConvertJSONToCSV(MemoryStream jsonStream) => _dataConverter.ConvertJsonToCsv(jsonStream);
        public Task<MemoryStream> ConvertJSONToYAML(MemoryStream jsonStream) => _dataConverter.ConvertJsonToYaml(jsonStream);
        public Task<MemoryStream> ConvertXMLToJSON(MemoryStream xmlStream) => _dataConverter.ConvertXmlToJson(xmlStream);
        public Task<MemoryStream> ConvertXMLToCSV(MemoryStream xmlStream) => _dataConverter.ConvertXmlToCsv(xmlStream);
        public Task<MemoryStream> ConvertXMLToYAML(MemoryStream xmlStream) => _dataConverter.ConvertXmlToYaml(xmlStream);
        public Task<MemoryStream> ConvertYAMLToJSON(MemoryStream yamlStream) => _dataConverter.ConvertYamlToJson(yamlStream);
        public Task<MemoryStream> ConvertYAMLToXML(MemoryStream yamlStream) => _dataConverter.ConvertYamlToXml(yamlStream);
        public Task<MemoryStream> ConvertTSVToCSV(MemoryStream tsvStream) => _dataConverter.ConvertTsvToCsv(tsvStream);
        public Task<MemoryStream> ConvertTSVToJSON(MemoryStream tsvStream) => _dataConverter.ConvertTsvToJson(tsvStream);

        #endregion

        #region Document Conversion Delegates

        public Task<MemoryStream> ConvertDocxToPdf(MemoryStream docxStream) => _documentConverter.ConvertDocxToPdf(docxStream);
        public Task<MemoryStream> ConvertDocxToHtml(MemoryStream docxStream) => _documentConverter.ConvertDocxToHtml(docxStream);
        public Task<MemoryStream> ConvertXLSXToCSV(MemoryStream xlsxStream) => _documentConverter.ConvertXlsxToCsv(xlsxStream);
        public Task<MemoryStream> ConvertXLSXToJSON(MemoryStream xlsxStream) => _documentConverter.ConvertXlsxToJson(xlsxStream);
        public Task<MemoryStream> ConvertXlsxToPdf(MemoryStream xlsxStream) => _documentConverter.ConvertXlsxToPdf(xlsxStream);
        public Task<MemoryStream> ConvertPptxToPdf(MemoryStream pptxStream) => _documentConverter.ConvertPptxToPdf(pptxStream);
        public Task<MemoryStream> ConvertPptxToPng(MemoryStream pptxStream) => _documentConverter.ConvertPptxToPng(pptxStream);
        public Task<MemoryStream> ConvertPptxToJpg(MemoryStream pptxStream) => _documentConverter.ConvertPptxToJpg(pptxStream);
        public Task<MemoryStream> ConvertMarkdownToHTML(MemoryStream markdownStream) => _documentConverter.ConvertMarkdownToHtml(markdownStream);
        public Task<MemoryStream> ConvertMarkdownToPdf(MemoryStream markdownStream) => _documentConverter.ConvertMarkdownToPdf(markdownStream);
        public Task<MemoryStream> ConvertEpubToPdf(MemoryStream epubStream) => _documentConverter.ConvertEpubToPdf(epubStream);
        public Task<MemoryStream> ConvertEpubToTxt(MemoryStream epubStream) => _documentConverter.ConvertEpubToTxt(epubStream);
        public Task<MemoryStream> ConvertHTMLToText(MemoryStream htmlStream) => _documentConverter.ConvertHtmlToText(htmlStream);
        public Task<MemoryStream> ConvertHtmlToPdf(MemoryStream htmlStream) => _documentConverter.ConvertHtmlToPdf(htmlStream);

        #endregion

        #region PDF Conversion Delegates

        public Task<MemoryStream> ConvertImageToPdf(MemoryStream imageStream) => _pdfConverter.ConvertImageToPdf(imageStream);
        public Task<MemoryStream> ConvertPdfToPng(MemoryStream pdfStream) => _pdfConverter.ConvertPdfToPng(pdfStream);
        public Task<MemoryStream> ConvertPdfToJpg(MemoryStream pdfStream) => _pdfConverter.ConvertPdfToJpg(pdfStream);
        public Task<MemoryStream> ConvertPdfToText(MemoryStream pdfStream) => _pdfConverter.ConvertPdfToText(pdfStream);
        public Task<MemoryStream> MergePdfsAsync(List<MemoryStream> pdfStreams) => _pdfConverter.MergePdfs(pdfStreams);
        public Task<MemoryStream> ExtractPdfPageAsync(MemoryStream pdfStream, int pageNumber) => _pdfConverter.ExtractPage(pdfStream, pageNumber);

        #endregion

        #region Archive Conversion Delegates

        public Task<MemoryStream> ConvertGzToTar(MemoryStream gzStream) => _archiveConverter.ConvertGzToTar(gzStream);
        public Task<MemoryStream> ConvertTarToGz(MemoryStream tarStream) => _archiveConverter.ConvertTarToGz(tarStream);
        public Task<MemoryStream> ConvertBz2ToTar(MemoryStream bz2Stream) => _archiveConverter.ConvertBz2ToTar(bz2Stream);
        public Task<MemoryStream> ConvertZipToTar(MemoryStream zipStream) => _archiveConverter.ConvertZipToTar(zipStream);
        public Task<MemoryStream> ConvertTarToZip(MemoryStream tarStream) => _archiveConverter.ConvertTarToZip(tarStream);
        public Task<MemoryStream> Convert7zToZip(MemoryStream sevenZipStream) => _archiveConverter.Convert7zToZip(sevenZipStream);
        public Task<MemoryStream> Convert7zToTar(MemoryStream sevenZipStream) => _archiveConverter.Convert7zToTar(sevenZipStream);
        public Task<MemoryStream> ConvertRarToZip(MemoryStream rarStream) => _archiveConverter.ConvertRarToZip(rarStream);
        public Task<MemoryStream> ConvertRarToTar(MemoryStream rarStream) => _archiveConverter.ConvertRarToTar(rarStream);

        #endregion

        #region Special Conversion Delegates

        public Task<MemoryStream> ConvertTextToQrCodePng(MemoryStream textStream) => _specialConverter.ConvertTextToQrCodePng(textStream);
        public Task<MemoryStream> ConvertTextToBarcodeJpg(MemoryStream textStream) => _specialConverter.ConvertTextToBarcodeJpg(textStream);

        #endregion

        #region RTF Conversion Delegates

        public Task<MemoryStream> ConvertRtfToHtml(MemoryStream rtfStream) => _documentConverter.ConvertRtfToHtml(rtfStream);
        public Task<MemoryStream> ConvertRtfToTxt(MemoryStream rtfStream) => _documentConverter.ConvertRtfToTxt(rtfStream);

        #endregion

        #region OpenDocument Conversion Delegates

        public Task<MemoryStream> ConvertOdtToDocx(MemoryStream odtStream) => _documentConverter.ConvertOdtToDocx(odtStream);
        public Task<MemoryStream> ConvertOdsToXlsx(MemoryStream odsStream) => _documentConverter.ConvertOdsToXlsx(odsStream);

        #endregion

        #region Legacy Methods (for backward compatibility)

        /// <summary>
        /// Legacy method - returns stream unchanged.
        /// </summary>
        public async Task<MemoryStream> ConvertDocToHTML(MemoryStream officeDocStream)
        {
            return await Task.FromResult(officeDocStream);
        }

        /// <summary>
        /// Splits a PDF into individual page PDFs.
        /// </summary>
        public async Task<List<MemoryStream>> SplitPdfAsync(MemoryStream pdfStream)
        {
            var resultStreams = new List<MemoryStream>();
            pdfStream.Position = 0;

            using (var document = UglyToad.PdfPig.PdfDocument.Open(pdfStream))
            {
                for (int i = 0; i < document.NumberOfPages; i++)
                {
                    var pageNumber = i + 1;
                    var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
                    builder.AddPage(document, pageNumber);

                    var pageBytes = builder.Build();
                    var pageStream = new MemoryStream(pageBytes);
                    pageStream.Position = 0;
                    resultStreams.Add(pageStream);
                }
            }

            return await Task.FromResult(resultStreams);
        }

        #endregion
    }
}
