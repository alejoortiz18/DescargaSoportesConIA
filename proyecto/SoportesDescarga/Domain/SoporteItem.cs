using System.Text.Json.Serialization;

namespace SoportesDescarga.Domain;

public class SoporteItem
{
    [JsonPropertyName("fechaRegistro")]
    public string? FechaRegistro { get; set; }

    [JsonPropertyName("storage_disk")]
    public string? StorageDisk { get; set; }

    [JsonPropertyName("storage_path")]
    public string? StoragePath { get; set; }
}
