using System.Text.Json.Serialization;

namespace SoportesDescarga.Domain;

public class ApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public List<SoporteItem>? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
