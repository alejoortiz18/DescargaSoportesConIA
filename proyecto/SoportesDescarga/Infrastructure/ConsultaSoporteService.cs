using System.Net.Http.Json;
using SoportesDescarga.Application.Interfaces;
using SoportesDescarga.Domain;

namespace SoportesDescarga.Infrastructure;

public class ConsultaSoporteService : IConsultaSoporteService
{
    private readonly HttpClient _http;

    public ConsultaSoporteService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(SoporteItem? Item, string? Motivo)> ConsultarAsync(
        string codigo, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(codigo, ct);

            var apiResponse = await response.Content
                .ReadFromJsonAsync<ApiResponse>(cancellationToken: ct);

            if (!response.IsSuccessStatusCode)
                return (null, apiResponse?.Message ?? $"Error HTTP {(int)response.StatusCode}");

            if (apiResponse is null || !apiResponse.Success
                || apiResponse.Data is null || apiResponse.Data.Count == 0)
                return (null, apiResponse?.Message ?? "Sin datos");

            return (apiResponse.Data[0], null);
        }
        catch (HttpRequestException ex)
        {
            return (null, $"Error de red: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (null, "Timeout API");
        }
    }
}
