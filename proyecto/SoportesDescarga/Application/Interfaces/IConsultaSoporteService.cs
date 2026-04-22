using SoportesDescarga.Domain;

namespace SoportesDescarga.Application.Interfaces;

public interface IConsultaSoporteService
{
    Task<(SoporteItem? Item, string? Motivo)> ConsultarAsync(string codigo, CancellationToken ct = default);
}
