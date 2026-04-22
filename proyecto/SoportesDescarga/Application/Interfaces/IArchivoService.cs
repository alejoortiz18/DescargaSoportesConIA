namespace SoportesDescarga.Application.Interfaces;

public interface IArchivoService
{
    Task<IEnumerable<string>> LeerEntradaAsync();
    Task<HashSet<string>> GetDescargadosExistentesAsync();
    Task AppendDescargadoAsync(string codigo);
    Task AppendFallidoAsync(string codigo, bool apiRespondio, string motivo);
}
