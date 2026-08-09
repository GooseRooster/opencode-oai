using System.Text.Json.Serialization;

namespace OpencodeOai.OpenCode.Models;

/// <summary>
/// Flat representation of an OpenCode message part. Only text and file parts
/// are consumed / emitted by the bridge. Unused fields for a given
/// <see cref="Type"/> are null. Mirrors <c>TextPartInput</c> and
/// <c>FilePartInput</c> from OpenCode's OpenAPI.
/// </summary>
public sealed class PartDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    // text
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    // file
    [JsonPropertyName("mime")]
    public string? Mime { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
}
