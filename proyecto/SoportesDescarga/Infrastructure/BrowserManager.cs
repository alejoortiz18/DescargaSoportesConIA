using Microsoft.Playwright;

namespace SoportesDescarga.Infrastructure;

public class BrowserManager : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    public IBrowserContext? Context { get; private set; }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 300
        });
        Context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Context is not null) await Context.DisposeAsync();
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
