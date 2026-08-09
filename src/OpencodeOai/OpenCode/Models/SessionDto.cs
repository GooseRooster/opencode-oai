using System.Text.Json.Serialization;

namespace OpencodeOai.OpenCode.Models;

public sealed class SessionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("time")]
    public SessionTimeDto? Time { get; set; }
}

public sealed class SessionTimeDto
{
    [JsonPropertyName("created")]
    public long Created { get; set; }
}

public sealed class CreateSessionRequest
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}
