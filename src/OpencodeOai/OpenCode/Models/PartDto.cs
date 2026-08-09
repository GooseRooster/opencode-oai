using System.Text.Json.Serialization;

namespace OpencodeOai.OpenCode.Models;

/// <summary>
/// Flat representation of an OpenCode message part.
/// Unused fields for a given <see cref="Type"/> are simply null.
/// Only text and image parts are consumed / emitted by the bridge.
/// </summary>
public sealed class PartDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("source")]
    public ImageSourceDto? Source { get; set; }
}

public sealed class ImageSourceDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
