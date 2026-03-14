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

### Supported Conversions

| From | To |
|------|-----|
| PNG, GIF, BMP, JPG, JPEG, JFIF | JPG/JPEG |
| PNG, GIF, BMP, JPG, JPEG, JFIF | PNG |
| PNG, GIF, BMP, JPG, JPEG, JFIF | GIF |
| CSV | XLSX |

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
