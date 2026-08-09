using System.Text.Json.Serialization;

namespace OpencodeOai.OpenCode.Models;

public sealed class ProvidersResponse
{
    [JsonPropertyName("connected")]
    public List<string>? Connected { get; set; }

    [JsonPropertyName("all")]
    public List<ProviderDto>? All { get; set; }
}

public sealed class ProviderDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("models")]
    public Dictionary<string, ProviderModelDto>? Models { get; set; }
}

public sealed class ProviderModelDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
