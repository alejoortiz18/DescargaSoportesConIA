using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using SoportesDescarga.Application.Interfaces;
using SoportesDescarga.Shared;

namespace SoportesDescarga.Infrastructure;

public class LoginService : ILoginService
{
    private readonly BrowserManager _browserManager;
    private readonly AppSettings _settings;
    private readonly SemaphoreSlim _loginSemaphore = new(1, 1);

    private IBrowserContext Context =>
        _browserManager.Context ?? throw new InvalidOperationException("Browser no inicializado.");

    public LoginService(BrowserManager browserManager, IOptions<AppSettings> settings)
    {
        _browserManager = browserManager;
        _settings = settings.Value;
    }

    public async Task LoginAsync(CancellationToken ct = default)
    {
        var page = await Context.NewPageAsync();
        try
        {
            await page.GotoAsync(_settings.Login.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000
            });

            await page.FillAsync("input[name='email']", _settings.Login.User);
            await page.FillAsync("input[name='password']", _settings.Login.Password);
            await page.ClickAsync("button[type='submit']");

            // NetworkIdle funciona para SPAs y apps tradicionales
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 30_000 });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task<bool> IsSessionActiveAsync(CancellationToken ct = default)
    {
        var baseUrl = ConfigHelper.GetIntranetBaseUrl(_settings.Login.Url);
        var page = await Context.NewPageAsync();
        try
        {
            await page.GotoAsync(baseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000
            });
            return !page.Url.Contains("/login");
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task EnsureSessionAsync(CancellationToken ct = default)
    {
        await _loginSemaphore.WaitAsync(ct);
        try
        {
            if (!await IsSessionActiveAsync(ct))
                await LoginAsync(ct);
        }
        finally
        {
            _loginSemaphore.Release();
        }
    }
}
