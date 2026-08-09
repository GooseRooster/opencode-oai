using System.Reflection;

namespace OpencodeOai.Endpoints;

/// <summary>Serves the hand-written OpenAPI 3.1 spec embedded as a resource.</summary>
internal sealed class OpenApiEndpoint : IEndpoint
{
    private const string ResourceName = "OpencodeOai.openapi.json";

    private static readonly Lazy<byte[]> SpecBytes = new(() =>
    {
        var asm = typeof(OpenApiEndpoint).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} not found");
        using var mem = new MemoryStream();
        stream.CopyTo(mem);
        return mem.ToArray();
    });

    public void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/openapi.json", (HttpContext ctx) =>
        {
            ctx.Response.ContentType = "application/json";
            return ctx.Response.Body.WriteAsync(SpecBytes.Value, 0, SpecBytes.Value.Length);
        }).AllowAnonymous();
    }
}
