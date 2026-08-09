using System.Text.Json.Serialization;
using OpencodeOai.OpenCode;
using OpencodeOai.Options;

namespace OpencodeOai.Endpoints;

internal sealed class HealthEndpoint : IEndpoint
{
    public const string BridgeVersion = "0.1.0";

    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (
            IOpenCodeClient client,
            Microsoft.Extensions.Options.IOptions<BridgeOptions> bridgeOpts,
            CancellationToken ct) =>
        {
            var bridge = bridgeOpts.Value;
            try
            {
                var upstream = await client.GetHealthAsync(ct);
                return Results.Json(new HealthResponse(
                    Status: "ok",
                    BridgeVersion: BridgeVersion,
                    Provider: bridge.DefaultProviderId,
                    OpenCode: new OpenCodeHealth(true, upstream.Version, null)
                ), HealthJsonContext.Default.HealthResponse);
            }
            catch (Exception ex)
            {
                return Results.Json(new HealthResponse(
                    Status: "ok",
                    BridgeVersion: BridgeVersion,
                    Provider: bridge.DefaultProviderId,
                    OpenCode: new OpenCodeHealth(false, null, ex.Message)
                ), HealthJsonContext.Default.HealthResponse);
            }
        }).AllowAnonymous();
    }
}

internal sealed record HealthResponse(
    string Status,
    string BridgeVersion,
    string Provider,
    OpenCodeHealth OpenCode);

internal sealed record OpenCodeHealth(
    bool Connected,
    string? Version,
    string? Error);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HealthResponse))]
internal partial class HealthJsonContext : JsonSerializerContext
{
}
