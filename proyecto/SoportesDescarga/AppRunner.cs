using Microsoft.Extensions.Options;
using SoportesDescarga.Application.Interfaces;
using SoportesDescarga.Shared;

namespace SoportesDescarga;

public class AppRunner
{
    private readonly IArchivoService _archivos;
    private readonly IConsultaSoporteService _consulta;
    private readonly IDescargaPdfService _descarga;
    private readonly AppSettings _settings;

    public AppRunner(
        IArchivoService archivos,
        IConsultaSoporteService consulta,
        IDescargaPdfService descarga,
        IOptions<AppSettings> settings)
    {
        _archivos = archivos;
        _consulta = consulta;
        _descarga = descarga;
        _settings = settings.Value;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var codigos = (await _archivos.LeerEntradaAsync()).ToList();
        var yaDescargados = await _archivos.GetDescargadosExistentesAsync();

        var outputDir = Path.GetFullPath(_settings.Paths.Output);
        var pendientes = codigos
            .Where(c => !yaDescargados.Contains(c))
            .Where(c => !File.Exists(Path.Combine(outputDir, $"{c}.pdf")))
            .ToList();

        int total = pendientes.Count;
        Console.WriteLine($"Total: {codigos.Count} | Ya descargados: {yaDescargados.Count} | Pendientes: {total}");
        Console.WriteLine(new string('-', 60));

        int i = 0;
        using var semaphore = new SemaphoreSlim(_settings.Concurrency.MaxParallelDownloads);

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

        Console.WriteLine(new string('-', 60));
        Console.WriteLine("Proceso finalizado.");
    }

    private async Task ProcesarCodigoAsync(
        string codigo, int num, int total, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        var (item, motivoConsulta) = await _consulta.ConsultarAsync(codigo, ct);

        if (item is null)
        {
            bool apiRespondio = motivoConsulta != null
                && !motivoConsulta.StartsWith("Error de red:")
                && motivoConsulta != "Timeout API";
            await _archivos.AppendFallidoAsync(codigo, apiRespondio, motivoConsulta ?? "Error desconocido");
            Console.WriteLine($"[{num}/{total}] {codigo} → {motivoConsulta}");
            return;
        }

        var (ok, motivoDescarga) = await _descarga.DescargarAsync(codigo, item.StoragePath!, ct);

        if (ok)
        {
            await _archivos.AppendDescargadoAsync(codigo);
            Console.WriteLine($"[{num}/{total}] {codigo} → Descargado");
        }
        else
        {
            await _archivos.AppendFallidoAsync(codigo, true, motivoDescarga ?? "Error desconocido");
            Console.WriteLine($"[{num}/{total}] {codigo} → FALLIDO: {motivoDescarga}");
        }
    }
}
