# FileConvert - Browser-Based File Conversion

A Blazor WebAssembly application for secure, client-side file conversion. All conversions happen in the browser - no files are uploaded to a server.

## Architecture

### Project Structure

- **FileConvert** - Blazor WebAssembly front-end application
- **FileConvert.Core** - Domain entities, value objects, and interfaces
- **FileConvert.Infrastructure** - File conversion implementations using ImageSharp and EPPlus
- **FileConvert.UnitTests** - Unit tests for conversion logic
- **FileConvert.UiTests** - Playwright-based UI tests
- **Infrastruture.Tester** - Development/testing console application

### Cross-Platform Support

This application is designed to run on:
- **WebAssembly** (all modern browsers) - primary deployment target
- **Linux, macOS, Windows** - supported for local development

Since the app runs in the browser via WebAssembly, CI only runs on `ubuntu-latest`.

### Dependencies

- **ImageSharp** (3.1.12) - Cross-platform image processing, WASM-compatible
- **EPPlus** (7.5.0) - Excel file handling, WASM-compatible
- **Markdig** (1.1.1) - Markdown processing, WASM-compatible
- **YamlDotNet** (16.3.0) - YAML serialization, WASM-compatible
- **HtmlAgilityPack** (1.12.4) - HTML parsing, WASM-compatible
- **SkiaSharp** (3.116.1) - Cross-platform 2D graphics, WASM-compatible
- **Svg.Skia** (2.0.0) - SVG rendering using SkiaSharp
- **SharpZipLib** (1.4.2) - Archive compression/decompression (ZIP, TAR, GZip, BZip2)
- **QuestPDF** (2024.12.0) - PDF generation, WASM-compatible via SkiaSharp

### Supported Conversions

| From | To |
|------|-----|
| PNG, GIF, BMP, JPG, JPEG, JFIF | JPG/JPEG |
| PNG, GIF, BMP, JPG, JPEG, JFIF | PNG |
| PNG, GIF, BMP, JPG, JPEG, JFIF | GIF |
| PNG, GIF, BMP, JPG, JPEG, JFIF | WebP |
| PNG, GIF, BMP, JPG, JPEG, JFIF, WebP, TIFF/TIF | ICO |
| PNG, GIF, BMP, JPG, JPEG, WebP | PDF |
| WebP | JPG/JPEG, PNG, GIF |
| TIFF/TIF | JPG/JPEG, PNG, WebP |
| ICO | PNG |
| SVG | PNG, JPG/JPEG, WebP |
| GZ/TGZ | TAR |
| TAR | GZ/TGZ, ZIP |
| BZ2/TBZ2 | TAR |
| ZIP | TAR |
| CSV | XLSX |
| CSV | JSON |
| CSV | YAML/YML |
| XLSX | CSV |
| XLSX | JSON |
| JSON | XML |
| JSON | CSV |
| JSON | YAML/YML |
| XML | JSON |
| XML | CSV |
| YAML/YML | JSON |
| Markdown (MD) | HTML |
| HTML | TXT |
| TSV | CSV |
| TSV | JSON |

## CI/CD

The GitHub Actions workflow runs on `ubuntu-latest` with:
- Playwright browser caching for faster CI runs
- Proper error handling for server startup

### Running Tests Locally

```bash
# Build
dotnet build --configuration Release

# Run unit tests
dotnet test FileConvert.UnitTests --configuration Release

# Run UI tests (requires app to be running)
cd FileConvert && dotnet run --urls "http://localhost:5000" &
dotnet test FileConvert.UiTests
```

## Development Notes

- The application uses .NET 10.0
- Blazor WebAssembly with PWA support
- Time zone support is enabled for consistent date handling
- Release builds use trimming for smaller bundle sizes
