namespace OpencodeOai.Endpoints;

/// <summary>
/// Explicit AOT-friendly endpoint registry. Add new endpoints to <see cref="Endpoints"/>.
/// </summary>
internal static class EndpointRegistry
{
    private static readonly IEndpoint[] Endpoints =
    [
        new HealthEndpoint(),
        new ModelsEndpoint(),
        new ChatCompletionsEndpoint(),
    ];

    public static void MapAll(IEndpointRouteBuilder app)
    {
        foreach (var endpoint in Endpoints)
        {
            endpoint.Map(app);
        }
    }
}
