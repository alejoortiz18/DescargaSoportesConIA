using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using SoportesDescarga.Application.Interfaces;
using SoportesDescarga.Shared;

namespace SoportesDescarga.Infrastructure;

public class DescargaPdfService : IDescargaPdfService
{
    private readonly BrowserManager _browserManager;
    private readonly ILoginService _loginService;
    private readonly AppSettings _settings;

    private IBrowserContext Context =>
        _browserManager.Context ?? throw new InvalidOperationException("Browser no inicializado.");

    public DescargaPdfService(
        BrowserManager browserManager,
        ILoginService loginService,
        IOptions<AppSettings> settings)
    {
        _browserManager = browserManager;
        _loginService = loginService;
        _settings = settings.Value;
    }

    public async Task<(bool Ok, string? Motivo)> DescargarAsync(
        string codigo, string storagePath, CancellationToken ct = default)
    {
        var baseUrl = ConfigHelper.GetIntranetBaseUrl(_settings.Login.Url);
        var url = $"{baseUrl}/ver-pdf/{storagePath}";
        var outputDir = Path.GetFullPath(_settings.Paths.Output);
        Directory.CreateDirectory(outputDir);
        var filePath = Path.Combine(outputDir, $"{codigo}.pdf");

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await _loginService.EnsureSessionAsync(ct);
            }
            catch (TimeoutException)
            {
                return (false, "Timeout al intentar login");
            }
            catch (PlaywrightException ex)
            {
                return (false, $"Error Playwright en login: {ex.Message}");
            }

            var page = await Context.NewPageAsync();
            bool sessionExpired = false;

            page.FrameNavigated += (_, frame) =>
            {
                if (frame.Url.Contains("/login"))
                    sessionExpired = true;
            };

            try
            {
                var downloadTask = page.WaitForDownloadAsync(
                    new PageWaitForDownloadOptions { Timeout = 30_000 });

                try
                {
                    await page.GotoAsync(url, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.Commit,
                        Timeout = 30_000
                    });
                }
                catch (PlaywrightException)
                {
                    // GotoAsync puede lanzar excepción cuando la respuesta es una descarga directa
                }

                if (sessionExpired)
                {
                    _ = downloadTask.ContinueWith(_ => { }, TaskContinuationOptions.None);
                    if (attempt == 0)
                    {
                        Console.WriteLine($"Sesión vencida, reintentando login...");
                        try
                        {
                            await _loginService.LoginAsync(ct);
                        }
                        catch (Exception ex)
                        {
                            return (false, $"Error al re-autenticar: {ex.Message}");
                        }
                        continue;
                    }
                    return (false, "Error descarga tras re-login");
                }

                IDownload download;
                try
                {
                    download = await downloadTask;
                }
                catch (TimeoutException)
                {
                    return (false, "Timeout esperando descarga del PDF");
                }
                catch (PlaywrightException ex)
                {
                    return (false, $"Error Playwright: {ex.Message}");
                }

                await download.SaveAsAsync(filePath);

                var info = new FileInfo(filePath);
                if (info.Length == 0)
                    return (false, "PDF en 0 bytes");

                return (true, null);
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        return (false, "Error descarga tras re-login");
    }
}
