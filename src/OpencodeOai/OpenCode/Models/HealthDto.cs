using System.Text.Json.Serialization;

namespace OpencodeOai.OpenCode.Models;

public sealed class HealthDto
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
