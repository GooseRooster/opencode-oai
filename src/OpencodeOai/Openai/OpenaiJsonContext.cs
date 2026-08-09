using System.Text.Json.Serialization;

namespace OpencodeOai.Openai;

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ModelList))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ChatCompletionRequest))]
[JsonSerializable(typeof(ChatCompletionResponse))]
[JsonSerializable(typeof(ChatChunk))]
internal partial class OpenaiJsonContext : JsonSerializerContext
{
}

public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorBody Error { get; set; } = new();
}

public sealed class ErrorBody
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "bridge_error";
}
