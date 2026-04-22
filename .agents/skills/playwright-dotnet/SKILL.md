---
name: playwright-dotnet
description: Use Microsoft Playwright in C#/.NET for browser automation, login session management, page navigation, intercepting network requests, and downloading files. Use when implementing LoginService, DescargaPdfService or any browser-driven automation in .NET.
---

# Microsoft Playwright en C#/.NET — Automatización de Navegador

Skill enfocado en el uso de Playwright .NET (`Microsoft.Playwright`) para automatización de sesiones, login, intercepción de respuestas y descarga de archivos.

## Instalación de paquete

```xml
<PackageReference Include="Microsoft.Playwright" Version="*" />
```

Instalar navegadores tras `dotnet build`:
```bash
playwright install chromium
```

## Setup básico — IPlaywright / IBrowser / IBrowserContext

```csharp
using Microsoft.Playwright;

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
            Headless = true,
        });
        Context = await _browser.NewContextAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Context is not null) await Context.DisposeAsync();
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
    }
}
```

## Patrón de Login y gestión de sesión

```csharp
public class LoginService : ILoginService
{
    private readonly IBrowserContext _context;
    private readonly IOptions<AppSettings> _settings;

    public LoginService(IBrowserContext context, IOptions<AppSettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    public async Task LoginAsync(CancellationToken ct = default)
    {
        var page = await _context.NewPageAsync();
        try
        {
            await page.GotoAsync(_settings.LoginUrl, new PageGotoOptions
            {
                Timeout = _settings.TimeoutMs
            });

            await page.FillAsync("input[name='email']", _settings.Username);
            await page.FillAsync("input[name='password']", _settings.Password);
            await page.ClickAsync("button[type='submit']");

            // Esperar redirección exitosa
            await page.WaitForURLAsync(url => !url.Contains("/login"),
                new PageWaitForURLOptions { Timeout = _settings.TimeoutMs });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task<bool> IsSessionActiveAsync(CancellationToken ct = default)
    {
        var page = await _context.NewPageAsync();
        try
        {
            await page.GotoAsync(_settings.ProtectedUrl, new PageGotoOptions
            {
                Timeout = _settings.TimeoutMs
            });
            return !page.Url.Contains("/login");
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public async Task EnsureSessionAsync(CancellationToken ct = default)
    {
        if (!await IsSessionActiveAsync(ct))
            await LoginAsync(ct);
    }
}
```

## Interceptar y descargar PDF con page.RouteAsync

```csharp
// Opción A: El PDF se sirve directamente desde la URL
public async Task<byte[]?> DownloadPdfDirectAsync(string pdfUrl, CancellationToken ct = default)
{
    var page = await _context.NewPageAsync();
    byte[]? pdfBytes = null;

    await page.RouteAsync("**/*.pdf", async route =>
    {
        var response = await route.FetchAsync();
        pdfBytes = await response.BodyAsync();
        await route.FulfillAsync(new RouteFulfillOptions
        {
            Response = response
        });
    });

    await page.GotoAsync(pdfUrl, new PageGotoOptions
    {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = 30_000
    });

    await page.CloseAsync();
    return pdfBytes;
}

// Opción B: La URL redirige a S3 — interceptar la URL firmada
public async Task<string?> GetS3SignedUrlAsync(string viewUrl, CancellationToken ct = default)
{
    var page = await _context.NewPageAsync();
    string? s3Url = null;

    page.Request += (_, request) =>
    {
        if (request.Url.Contains("amazonaws.com") || request.Url.Contains("s3."))
            s3Url = request.Url;
    };

    await page.GotoAsync(viewUrl, new PageGotoOptions
    {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = 30_000
    });

    await page.CloseAsync();
    return s3Url;
}
```

## Detectar redirección al login durante la descarga

```csharp
public async Task<(byte[]? Bytes, string? Error)> DownloadWithSessionGuardAsync(
    string url,
    ILoginService loginService,
    CancellationToken ct = default)
{
    for (int attempt = 0; attempt < 2; attempt++)
    {
        await loginService.EnsureSessionAsync(ct);

        var page = await _context.NewPageAsync();
        byte[]? bytes = null;
        bool redirectedToLogin = false;

        page.Response += async (_, response) =>
        {
            if (response.Url.Contains("/login"))
                redirectedToLogin = true;
        };

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30_000
            });

            if (redirectedToLogin)
            {
                await loginService.LoginAsync(ct);
                continue; // reintento
            }

            // Capturar respuesta con RouteAsync (ver patrón arriba)
            // ...

            return (bytes, null);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    return (null, "Error descarga tras re-login");
}
```

## Patrones de selección de elementos

```csharp
// Por rol (preferido para accesibilidad)
await page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
await page.GetByRole(AriaRole.Link, new() { Name = "Descargar" }).ClickAsync();

// Por texto
await page.GetByText("Iniciar sesión").ClickAsync();

// Por selector CSS
await page.FillAsync("input[name='usuario']", "miusuario");
await page.ClickAsync("button[type='submit']");

// Por placeholder
await page.GetByPlaceholder("Contraseña").FillAsync("mipassword");
```

## Timeouts recomendados

```csharp
// En appsettings.json
{
  "App": {
    "LoginTimeoutMs": 15000,
    "DownloadTimeoutMs": 30000
  }
}

// En código
await page.GotoAsync(url, new PageGotoOptions { Timeout = _settings.LoginTimeoutMs });
```

## Buenas prácticas

1. **Reusar IBrowserContext** entre operaciones — mantiene las cookies de sesión
2. **Crear y cerrar IPage** por operación — evita memory leaks
3. **Siempre llamar EnsureSessionAsync()** antes de cada descarga
4. **Usar CancellationToken** en todos los métodos async
5. **Implementar IAsyncDisposable** en clases que wrappean IPlaywright/IBrowser
6. **Timeout máximo 15 seg para login**, 30 seg para descarga de PDF
7. **Headless=true** en producción, **Headless=false** para depuración local
8. **No usar Playwright para llamadas API** — usar HttpClient directamente

## Errores comunes

- `TimeoutException`: aumentar timeout o verificar selector
- Sesión expirada: verificar que `EnsureSessionAsync()` se llama antes de la operación
- PDF en 0 bytes: verificar que `RouteAsync` captura la respuesta antes del cierre de página
- Cookies no persistidas: asegurarse de usar el mismo `IBrowserContext` en todas las operaciones
