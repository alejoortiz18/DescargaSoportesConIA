namespace SoportesDescarga.Application.Interfaces;

public interface ILoginService
{
    Task LoginAsync(CancellationToken ct = default);
    Task<bool> IsSessionActiveAsync(CancellationToken ct = default);
    Task EnsureSessionAsync(CancellationToken ct = default);
}
