namespace SoportesDescarga.Application.Interfaces;

public interface IDescargaPdfService
{
    Task<(bool Ok, string? Motivo)> DescargarAsync(string codigo, string storagePath, CancellationToken ct = default);
}
