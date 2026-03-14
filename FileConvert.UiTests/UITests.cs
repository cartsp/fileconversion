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
        /// </summary>
        private async Task NavigateAndWaitForBlazorAsync()
        {
            await _fixture.Page.GotoAsync(BaseUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await WaitForBlazorReadyAsync();
        }

        /// <summary>
        /// Comprehensive UI test that validates all UI functionality in a single page load.
        /// This approach avoids Blazor WASM reinitialization issues in CI environments.
        /// </summary>
        [Fact]
        public async Task TestBlazorAppUI()
        {
            // Step 1: Navigate and verify page loads
            await NavigateAndWaitForBlazorAsync();
            var pageTitle = await _fixture.Page.TitleAsync();
            Assert.Equal("Browser Based File Conversion Tools", pageTitle);

            // Step 2: Verify app starts up with expected elements
            var fileLabel = await WaitForElementWithRetryAsync("#file-label");
            Assert.NotNull(fileLabel);

            // Step 3: Verify file control exists
            var fileControl = await WaitForElementWithRetryAsync("#file-1");
            Assert.NotNull(fileControl);

            // Step 4: Test available file conversion appears (CSV -> XLSX)
            var uploadElement = await WaitForElementWithRetryAsync("#file-1");
            var filepath = Path.Combine(Directory.GetCurrentDirectory(), "Documents", "cities.csv");
            await uploadElement!.SetInputFilesAsync(filepath);

            await _fixture.Page.WaitForSelectorAsync(".conversion-choices", new() { Timeout = ElementTimeout, State = WaitForSelectorState.Attached });
            var conversionSelections = await _fixture.Page.QuerySelectorAllAsync(".conversion-choices");
            Assert.NotEmpty(conversionSelections);
            var htmlOption = await conversionSelections[0].TextContentAsync();
            Assert.Equal(FileExtension.xlsx, htmlOption);
        }
    }
}
