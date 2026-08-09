using System.Text.Json.Serialization;
using OpencodeOai.OpenCode.Models;

namespace OpencodeOai;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(List<SessionDto>))]
[JsonSerializable(typeof(CreateSessionRequest))]
[JsonSerializable(typeof(SendMessageRequest))]
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(ProvidersResponse))]
[JsonSerializable(typeof(PartDto))]
[JsonSerializable(typeof(ImageSourceDto))]
[JsonSerializable(typeof(ProviderDto))]
[JsonSerializable(typeof(ModelRef))]
internal partial class OpenCodeJsonContext : JsonSerializerContext
{
}
