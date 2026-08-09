using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpencodeOai.Auth;

/// <summary>Writes an OpenAI-compatible error body for 401 responses.</summary>
internal static class ApiKeyChallenge
{
    public static Task WriteAsync(HttpContext ctx)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        ctx.Response.ContentType = "application/json";
        var payload = new AuthError(new AuthErrorBody("Unauthorized", "auth_error"));
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, AuthErrorJsonContext.Default.AuthError));
    }
}

internal sealed record AuthError(
    [property: JsonPropertyName("error")] AuthErrorBody Error);

internal sealed record AuthErrorBody(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("type")]    string Type);

[JsonSerializable(typeof(AuthError))]
internal partial class AuthErrorJsonContext : JsonSerializerContext
{
}
