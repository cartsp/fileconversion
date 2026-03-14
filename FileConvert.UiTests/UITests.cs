using FileConvert.Core.ValueObjects;
using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace FileConvert.UiTests
{
    public class UiTests : IClassFixture<PlaywrightFixture>
    {
        private readonly PlaywrightFixture _fixture;
        private static readonly string BaseUrl = System.Environment.GetEnvironmentVariable("TEST_URL") ?? "http://localhost:5100";

        // CI environments need longer timeouts
        private static readonly int BlazorLoadTimeout = System.Environment.GetEnvironmentVariable("CI") != null ? 120000 : 90000;
        private static readonly int ElementTimeout = System.Environment.GetEnvironmentVariable("CI") != null ? 20000 : 10000;

        public UiTests(PlaywrightFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// Waits for an element with retry logic for resilience against transient failures.
        /// </summary>
        private async Task<IElementHandle> WaitForElementWithRetryAsync(string selector, int timeout = 0)
        {
            var effectiveTimeout = timeout > 0 ? timeout : ElementTimeout;
            Exception? lastException = null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    return await _fixture.Page.WaitForSelectorAsync(selector, new()
                    {
                        Timeout = effectiveTimeout,
                        State = WaitForSelectorState.Attached
                    });
                }
                catch (PlaywrightException ex) when (attempt < 2)
                {
                    lastException = ex;
                    await Task.Delay(500 * (attempt + 1));
                }
            }
            throw lastException!;
        }

        /// <summary>
        /// Waits for Blazor WASM to be fully ready (loading indicator hidden).
        /// </summary>
        private async Task WaitForBlazorReadyAsync()
        {
            await _fixture.Page.WaitForSelectorAsync(".splash-loading", new()
            {
                State = WaitForSelectorState.Hidden,
                Timeout = BlazorLoadTimeout
            });
        }

        /// <summary>
        /// Navigates to the page and waits for Blazor WASM to fully initialize.
        /// Blazor shows a loading indicator while initializing - we wait for it to disappear.
        /// Creates a fresh browser context for each test to ensure isolation.
        /// </summary>
        private async Task NavigateAndWaitForBlazorAsync()
        {
            // Create a fresh context for each test - Blazor WASM doesn't handle
            // page reloads well in the same browser context
            await _fixture.CreateNewContextAsync();
            await _fixture.Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await WaitForBlazorReadyAsync();
        }

        [Fact]
        public async Task TestCanOpenSite()
        {
            // Arrange - create fresh context for test isolation
            await _fixture.CreateNewContextAsync();
            await _fixture.Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

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
            var fileLabel = await WaitForElementWithRetryAsync("#file-label");

            // Assert
            Assert.NotNull(fileLabel);
        }

        [Fact]
        public async Task TestFileControlExists()
        {
            // Arrange
            await NavigateAndWaitForBlazorAsync();

            // Act
            var fileControl = await WaitForElementWithRetryAsync("#file-1");

            // Assert
            Assert.NotNull(fileControl);
        }

        [Fact]
        public async Task TestAvailableFileConversionAppears()
        {
            // Arrange
            await NavigateAndWaitForBlazorAsync();
            var uploadElement = await WaitForElementWithRetryAsync("#file-1");

            var filepath = Path.Combine(Directory.GetCurrentDirectory(), "Documents", "cities.csv");
            await uploadElement!.SetInputFilesAsync(filepath);

            // Act - wait for the conversion choice to be attached (it's an option in a select, may not be visible)
            await _fixture.Page.WaitForSelectorAsync(".conversion-choices", new() { Timeout = ElementTimeout, State = WaitForSelectorState.Attached });
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
            var uploadElement = await WaitForElementWithRetryAsync("#file-1");

            var filepath = Path.Combine(Directory.GetCurrentDirectory(), "Documents", "test.dgn");
            await uploadElement!.SetInputFilesAsync(filepath);

            // Act
            var noConversionsFound = await _fixture.Page.WaitForSelectorAsync(".no-convertors-found", new() { Timeout = ElementTimeout });
            var textContent = await noConversionsFound!.TextContentAsync();

            // Assert - trim whitespace from the text content
            Assert.NotNull(noConversionsFound);
            Assert.Equal("No file conversions available for this file type", textContent?.Trim());
        }
    }
}
