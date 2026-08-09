using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OpencodeOai.Auth;
using OpencodeOai.OpenCode;
using OpencodeOai.Openai;
using OpencodeOai.Options;

namespace OpencodeOai.Endpoints;

internal sealed class ModelsEndpoint : IEndpoint
{
    // Reasonable fallback set used when /provider is unreachable.
    private static readonly string[] Fallback =
    [
        "github-copilot/gpt-4o",
        "github-copilot/gpt-4.1",
        "github-copilot/claude-sonnet-4-5",
        "github-copilot/gpt-5-mini",
    ];

    private static ModelList? _lastKnownGood;

    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/models", async (
            IOpenCodeClient client,
            IOptions<BridgeOptions> bridge,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Models");
            try
            {
                var providers = await client.GetProvidersAsync(ct);
                var connected = new HashSet<string>(providers.Connected ?? new(), StringComparer.Ordinal);
                var list = new ModelList();

                foreach (var p in providers.All ?? new())
                {
                    if (!connected.Contains(p.Id) || p.Models is null) continue;
                    foreach (var modelId in p.Models.Keys)
                    {
                        list.Data.Add(new ModelListEntry
                        {
                            Id = $"{p.Id}/{modelId}",
                            OwnedBy = p.Id,
                        });
                    }
                }

                if (list.Data.Count == 0) throw new InvalidOperationException("No connected providers found");

                _lastKnownGood = list;
                logger.LogDebug("Returning {Count} models", list.Data.Count);
                return Results.Json(list, OpenaiJsonContext.Default.ModelList);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch models, returning fallback");
                var list = _lastKnownGood ?? BuildFallback();
                return Results.Json(list, OpenaiJsonContext.Default.ModelList);
            }
        })
        .RequireAuthorization(ApiKeyAuthHandler.SchemeName);
    }

    private static ModelList BuildFallback()
    {
        var list = new ModelList();
        foreach (var id in Fallback)
        {
            var owner = id.Split('/', 2)[0];
            list.Data.Add(new ModelListEntry { Id = id, OwnedBy = owner });
        }
        return list;
    }
}
