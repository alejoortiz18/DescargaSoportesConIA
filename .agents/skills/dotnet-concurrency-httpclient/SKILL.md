---
name: dotnet-concurrency-httpclient
description: Patrones de concurrencia con SemaphoreSlim, HttpClient con Bearer token, retry helpers, y CancellationToken para aplicaciones .NET console. Usar en AppRunner.cs, RetryHelper.cs y ConsultaSoporteService.cs de este proyecto.
---

# Concurrencia y HttpClient en .NET — Patrones para Descarga Masiva

## SemaphoreSlim para limitar concurrencia

```csharp
public class AppRunner
{
    private readonly IArchivoService _archivos;
    private readonly IConsultaSoporteService _consulta;
    private readonly IDescargaPdfService _descarga;
    private const int MaxConcurrency = 3;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var codigos = await _archivos.LeerEntradaAsync();
        var yaDescargados = await _archivos.GetDescargadosExistentesAsync();
        var pendientes = codigos.Except(yaDescargados).ToList();

        int total = pendientes.Count;
        int i = 0;

        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tareas = pendientes.Select(async codigo =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var num = Interlocked.Increment(ref i);
                await ProcesarCodigoAsync(codigo, num, total, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tareas);
    }

    private async Task ProcesarCodigoAsync(
        string codigo, int num, int total, CancellationToken ct)
    {
        Console.WriteLine($"[{num}/{total}] {codigo} → procesando...");

        var soporte = await _consulta.ConsultarAsync(codigo, ct);
        if (soporte is null)
        {
            await _archivos.AppendFallidoAsync(codigo, "No encontrado");
            Console.WriteLine($"[{num}/{total}] {codigo} → FALLIDO: No encontrado");
            return;
        }

        var resultado = await _descarga.DescargarAsync(codigo, soporte.StoragePath, ct);
        if (resultado.Ok)
        {
            await _archivos.AppendDescargadoAsync(codigo);
            Console.WriteLine($"[{num}/{total}] {codigo} → OK");
        }
        else
        {
            await _archivos.AppendFallidoAsync(codigo, resultado.Motivo ?? "Error desconocido");
            Console.WriteLine($"[{num}/{total}] {codigo} → FALLIDO: {resultado.Motivo}");
        }
    }
}
```

## HttpClient con Bearer Token

```csharp
// Registro en DI
services.AddHttpClient<IConsultaSoporteService, ConsultaSoporteService>(client =>
{
    client.BaseAddress = new Uri(configuration["Api:BaseUrl"]!);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", configuration["Api:Token"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Implementación del servicio
public class ConsultaSoporteService : IConsultaSoporteService
{
    private readonly HttpClient _http;
    private readonly ILogger<ConsultaSoporteService> _logger;

    public ConsultaSoporteService(HttpClient http, ILogger<ConsultaSoporteService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<SoporteItem?> ConsultarAsync(string codigo, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/api/v1/consultasoporte/{codigo}", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API retornó {StatusCode} para código {Codigo}",
                    response.StatusCode, codigo);
                return null;
            }

            var apiResponse = await response.Content
                .ReadFromJsonAsync<ApiResponse>(cancellationToken: ct);

            if (apiResponse is null || !apiResponse.Success)
            {
                _logger.LogWarning("API indicó success=false para código {Codigo}", codigo);
                return null;
            }

            return apiResponse.Data;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de red consultando código {Codigo}", codigo);
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout consultando código {Codigo}", codigo);
            return null;
        }
    }
}
```

## RetryHelper — 1 reintento para errores transitorios

```csharp
public static class RetryHelper
{
    public static async Task<T?> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T?>> operation,
        CancellationToken ct = default,
        int maxRetries = 1,
        int delayMs = 1000)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await operation(ct);
                if (result is not null) return result;
            }
            catch (HttpRequestException) when (attempt < maxRetries)
            {
                await Task.Delay(delayMs, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < maxRetries)
            {
                await Task.Delay(delayMs, ct);
            }
        }
        return default;
    }
}
```

## Manejo de CancellationToken con Ctrl+C

```csharp
// En Program.cs
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // evita terminar el proceso inmediatamente
    cts.Cancel();
    Console.WriteLine("\nCancelando... espere a que el proceso escriba el estado.");
};

try
{
    await appRunner.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Proceso cancelado por el usuario.");
}
```

## Patrones de archivo — append thread-safe

```csharp
// Para escrituras concurrentes en archivos de log, usar SemaphoreSlim
public class ArchivoService : IArchivoService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _descargadosPath = "descargados.txt";
    private readonly string _fallidosPath = "fallidos.txt";

    public async Task AppendDescargadoAsync(string codigo)
    {
        await _writeLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_descargadosPath, codigo + Environment.NewLine);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AppendFallidoAsync(string codigo, string motivo)
    {
        await _writeLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_fallidosPath,
                $"{codigo} | {motivo}{Environment.NewLine}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<HashSet<string>> GetDescargadosExistentesAsync()
    {
        if (!File.Exists(_descargadosPath))
            return new HashSet<string>();

        var lines = await File.ReadAllLinesAsync(_descargadosPath);
        return lines.Where(l => !string.IsNullOrWhiteSpace(l))
                    .Select(l => l.Trim())
                    .ToHashSet();
    }
}
```

## Buenas prácticas

1. **SemaphoreSlim** en lugar de `Parallel.ForEachAsync` para control fino de concurrencia
2. **Siempre liberar** `SemaphoreSlim` en bloque `finally`
3. **No usar** `new HttpClient()` — registrar siempre con `IHttpClientFactory`
4. **Distinguir** `TaskCanceledException` por timeout vs. por `ct.IsCancellationRequested`
5. **Mutex en escritura de archivos** — múltiples tasks pueden escribir simultáneamente
6. **Guardar estado** ante Ctrl+C — siempre terminar el ciclo actual antes de salir
