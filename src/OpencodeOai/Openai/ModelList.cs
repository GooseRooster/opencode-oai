using System.Text.Json.Serialization;

namespace OpencodeOai.Openai;

public sealed class ModelList
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";

    [JsonPropertyName("data")]
    public List<ModelListEntry> Data { get; set; } = new();
}

public sealed class ModelListEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("object")]
    public string Object { get; set; } = "model";

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; set; } = "";

    [JsonPropertyName("created")]
    public long Created { get; set; }
}
