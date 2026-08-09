using System.Text.Json.Serialization;

namespace OpencodeOai.OpenCode.Models;

public sealed class SendMessageRequest
{
    [JsonPropertyName("model")]
    public ModelRef Model { get; set; } = new();

    [JsonPropertyName("parts")]
    public List<PartDto> Parts { get; set; } = new();
}

public sealed class ModelRef
{
    [JsonPropertyName("providerID")]
    public string ProviderId { get; set; } = "";

    [JsonPropertyName("modelID")]
    public string ModelId { get; set; } = "";
}

public sealed class MessageResponse
{
    [JsonPropertyName("parts")]
    public List<PartDto>? Parts { get; set; }

    [JsonPropertyName("info")]
    public MessageInfo? Info { get; set; }
}

public sealed class MessageInfo
{
    [JsonPropertyName("tokens")]
    public TokenUsage? Tokens { get; set; }
}

public sealed class TokenUsage
{
    [JsonPropertyName("input")]
    public int Input { get; set; }

    [JsonPropertyName("output")]
    public int Output { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
