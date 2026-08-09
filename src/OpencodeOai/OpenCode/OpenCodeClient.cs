using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpencodeOai.OpenCode.Models;
using OpencodeOai.Options;

namespace OpencodeOai.OpenCode;

internal sealed class OpenCodeClient : IOpenCodeClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenCodeClient> _logger;

    public OpenCodeClient(HttpClient http, IOptions<OpenCodeOptions> options, ILogger<OpenCodeClient> logger)
    {
        _http = http;
        _logger = logger;

        var opts = options.Value;
        _http.BaseAddress = new Uri(opts.Url.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromMilliseconds(opts.TimeoutMs);

        if (!string.IsNullOrEmpty(opts.Password))
        {
            var raw = Encoding.UTF8.GetBytes($"{opts.Username}:{opts.Password}");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
        }
    }

    public Task<HealthDto> GetHealthAsync(CancellationToken ct = default) =>
        GetJsonAsync("global/health", OpenCodeJsonContext.Default.HealthDto, ct);

    public Task<ProvidersResponse> GetProvidersAsync(CancellationToken ct = default) =>
        GetJsonAsync("provider", OpenCodeJsonContext.Default.ProvidersResponse, ct);

    public Task<SessionDto> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default) =>
        PostJsonAsync("session", req,
            OpenCodeJsonContext.Default.CreateSessionRequest,
            OpenCodeJsonContext.Default.SessionDto, ct);

    public Task<MessageResponse> SendMessageAsync(string sessionId, SendMessageRequest req, CancellationToken ct = default) =>
        PostJsonAsync($"session/{Uri.EscapeDataString(sessionId)}/message", req,
            OpenCodeJsonContext.Default.SendMessageRequest,
            OpenCodeJsonContext.Default.MessageResponse, ct);

    public Task<List<SessionDto>> ListSessionsAsync(CancellationToken ct = default) =>
        GetJsonAsync("session", OpenCodeJsonContext.Default.ListSessionDto, ct);

    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.DeleteAsync($"session/{Uri.EscapeDataString(sessionId)}", ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "delete session {SessionId} failed", sessionId);
            return false;
        }
    }

    private async Task<T> GetJsonAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info, CancellationToken ct)
    {
        using var res = await _http.GetAsync(path, ct);
        await EnsureSuccess(path, res, ct);
        var value = await res.Content.ReadFromJsonAsync(info, ct);
        return value ?? throw new OpenCodeException($"OpenCode GET {path} → empty response");
    }

    private async Task<TRes> PostJsonAsync<TReq, TRes>(
        string path,
        TReq body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TReq> reqInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRes> resInfo,
        CancellationToken ct)
    {
        using var content = JsonContent.Create(body, reqInfo);
        using var res = await _http.PostAsync(path, content, ct);
        await EnsureSuccess(path, res, ct);
        var value = await res.Content.ReadFromJsonAsync(resInfo, ct);
        return value ?? throw new OpenCodeException($"OpenCode POST {path} → empty response");
    }

    private static async Task EnsureSuccess(string path, HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = await res.Content.ReadAsStringAsync(ct);
        var trimmed = body.Length > 300 ? body[..300] : body;
        throw new OpenCodeException($"OpenCode {res.RequestMessage?.Method} {path} → {(int)res.StatusCode}: {trimmed}", (int)res.StatusCode);
    }
}

public sealed class OpenCodeException : Exception
{
    public int? StatusCode { get; }
    public OpenCodeException(string message, int? statusCode = null) : base(message) => StatusCode = statusCode;
}
