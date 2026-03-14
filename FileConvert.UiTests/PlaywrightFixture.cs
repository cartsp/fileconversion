using Microsoft.Playwright;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FileConvert.UiTests;

public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
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

        Page = await Browser.NewPageAsync();

        // Configure page timeouts for CI environments
        Page.SetDefaultTimeout(DefaultTimeout);
        Page.SetDefaultNavigationTimeout(NavigationTimeout);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await Page.CloseAsync();
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
