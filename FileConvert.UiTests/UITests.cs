using FileConvert.Core.ValueObjects;
using Microsoft.Playwright;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FileConvert.UiTests
{
    public class UiTests : IClassFixture<PlaywrightFixture>
    {
        private readonly PlaywrightFixture _fixture;
        private static readonly string BaseUrl = System.Environment.GetEnvironmentVariable("TEST_URL") ?? "http://localhost:5100";

        public UiTests(PlaywrightFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// Navigates to the page and waits for Blazor WASM to fully initialize.
        /// Blazor shows a loading indicator while initializing - we wait for it to disappear.
        /// </summary>
        private async Task NavigateAndWaitForBlazorAsync()
        {
            await _fixture.Page.GotoAsync(BaseUrl);
            // Wait for the loading indicator to disappear (Blazor replaces it with actual content)
            await _fixture.Page.WaitForSelectorAsync(".splash-loading", new() { State = WaitForSelectorState.Hidden, Timeout = 90000 });
        }

        [Fact]
        public async Task TestCanOpenSite()
        {
            // Arrange
            await _fixture.Page.GotoAsync(BaseUrl);

            // Act
            var pageTitle = await _fixture.Page.TitleAsync();

            // Assert
            Assert.Equal("Browser Based File Conversion Tools", pageTitle);
        }

        [Fact]
        public async Task TestAppStartsUp()
        {
            // Arrange
            await NavigateAndWaitForBlazorAsync();

            // Act
            var fileLabel = await _fixture.Page.WaitForSelectorAsync("#file-label", new() { Timeout = 10000 });

            // Assert
            Assert.NotNull(fileLabel);
        }

        [Fact]
        public async Task TestFileControlExists()
        {
            // Arrange
            await NavigateAndWaitForBlazorAsync();

            // Act
            var fileControl = await _fixture.Page.WaitForSelectorAsync("#file-1", new() { Timeout = 10000 });

            // Assert
            Assert.NotNull(fileControl);
        }

        [Fact]
        public async Task TestAvailableFileConversionAppears()
        {
            // Arrange
            await NavigateAndWaitForBlazorAsync();
            var uploadElement = await _fixture.Page.WaitForSelectorAsync("#file-1", new() { Timeout = 10000 });

            var filepath = Path.Combine(Directory.GetCurrentDirectory(), "Documents", "cities.csv");
            await uploadElement!.SetInputFilesAsync(filepath);

            // Act - wait for the conversion choice to be attached (it's an option in a select, may not be visible)
            await _fixture.Page.WaitForSelectorAsync(".conversion-choices", new() { Timeout = 10000, State = WaitForSelectorState.Attached });
            var conversionSelections = await _fixture.Page.QuerySelectorAllAsync(".conversion-choices");

            // Assert
            Assert.NotEmpty(conversionSelections);
            var htmlOption = await conversionSelections[0].TextContentAsync();
            Assert.Equal(FileExtension.xlsx, htmlOption);
        }

        [Fact]
        public async Task TestNoAvailableFileConversionAppears()
        {
            // Arrange
            await NavigateAndWaitForBlazorAsync();
            var uploadElement = await _fixture.Page.WaitForSelectorAsync("#file-1", new() { Timeout = 10000 });

            var filepath = Path.Combine(Directory.GetCurrentDirectory(), "Documents", "test.dgn");
            await uploadElement!.SetInputFilesAsync(filepath);

            // Act
            var noConversionsFound = await _fixture.Page.WaitForSelectorAsync(".no-convertors-found", new() { Timeout = 10000 });
            var textContent = await noConversionsFound!.TextContentAsync();

            // Assert - trim whitespace from the text content
            Assert.NotNull(noConversionsFound);
            Assert.Equal("No file conversions available for this file type", textContent?.Trim());
        }
    }
}
