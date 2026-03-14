using Microsoft.Playwright;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FileConvert.UiTests;

public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    private IBrowserContext _context = null!;
    public IPage Page { get; private set; } = null!;

    // CI environments need longer timeouts
    private static readonly int DefaultTimeout = System.Environment.GetEnvironmentVariable("CI") != null ? 30000 : 10000;
    private static readonly int NavigationTimeout = System.Environment.GetEnvironmentVariable("CI") != null ? 60000 : 30000;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        // Retry browser launch up to 3 times for flakiness resilience
        PlaywrightException? lastException = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Browser = await Playwright.Chromium.LaunchAsync(new()
                {
                    Headless = !System.Diagnostics.Debugger.IsAttached,
                    Timeout = NavigationTimeout
                });
                break;
            }
            catch (PlaywrightException ex) when (attempt < 2)
            {
                lastException = ex;
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        if (Browser == null)
        {
            throw new InvalidOperationException("Failed to launch browser after 3 attempts", lastException);
        }

        await CreateNewContextAsync();
    }

    /// <summary>
    /// Creates a fresh browser context and page.
    /// Call this before each test to ensure isolation - Blazor WASM doesn't handle
    /// page reloads well in the same browser context.
    /// </summary>
    public async Task CreateNewContextAsync()
    {
        if (_context != null)
        {
            await _context.CloseAsync();
        }
        _context = await Browser.NewContextAsync();
        Page = await _context.NewPageAsync();

        // Configure page timeouts for CI environments
        Page.SetDefaultTimeout(DefaultTimeout);
        Page.SetDefaultNavigationTimeout(NavigationTimeout);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_context != null)
            {
                await _context.CloseAsync();
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }

        try
        {
            await Browser.CloseAsync();
        }
        catch
        {
            // Ignore errors during cleanup
        }

        Playwright.Dispose();
    }
}
