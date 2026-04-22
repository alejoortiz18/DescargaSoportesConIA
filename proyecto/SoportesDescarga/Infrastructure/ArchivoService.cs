using Microsoft.Extensions.Options;
using SoportesDescarga.Application.Interfaces;
using SoportesDescarga.Shared;

namespace SoportesDescarga.Infrastructure;

public class ArchivoService : IArchivoService
{
    private readonly AppSettings _settings;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private string WwwrootPath => Path.GetDirectoryName(Path.GetFullPath(_settings.Paths.Output))!;
    private string DescargadosPath => Path.Combine(WwwrootPath, "descargados.txt");
    private string FallidosPath => Path.Combine(WwwrootPath, "fallidos.csv");

    public ArchivoService(IOptions<AppSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<IEnumerable<string>> LeerEntradaAsync()
    {
        var inputPath = Path.GetFullPath(_settings.Paths.Input);
        if (!File.Exists(inputPath))
            return Enumerable.Empty<string>();

        var lines = await File.ReadAllLinesAsync(inputPath);
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .Distinct();
    }

    public async Task<HashSet<string>> GetDescargadosExistentesAsync()
    {
        if (!File.Exists(DescargadosPath))
            return new HashSet<string>();

        var lines = await File.ReadAllLinesAsync(DescargadosPath);
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .ToHashSet();
    }

    public async Task AppendDescargadoAsync(string codigo)
    {
        await _writeLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(WwwrootPath);
            await File.AppendAllTextAsync(DescargadosPath, codigo + Environment.NewLine);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AppendFallidoAsync(string codigo, bool apiRespondio, string motivo)
    {
        await _writeLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(WwwrootPath);
            bool isNew = !File.Exists(FallidosPath);
            await using var writer = new StreamWriter(FallidosPath, append: true, System.Text.Encoding.UTF8);
            if (isNew)
                await writer.WriteLineAsync("documento;apiRespondio;mensaje");
            var mensajeSanitizado = motivo.Replace("\"", "\"\"");
            await writer.WriteLineAsync($"{codigo};{apiRespondio.ToString().ToLower()};\"{mensajeSanitizado}\"");
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
