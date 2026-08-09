using OpencodeOai.OpenCode.Models;

namespace OpencodeOai.OpenCode;

/// <summary>Narrow, hand-written surface of the OpenCode REST API that this bridge consumes.</summary>
public interface IOpenCodeClient
{
    Task<HealthDto> GetHealthAsync(CancellationToken ct = default);

    Task<ProvidersResponse> GetProvidersAsync(CancellationToken ct = default);

    Task<SessionDto> CreateSessionAsync(CreateSessionRequest req, CancellationToken ct = default);

    Task<MessageResponse> SendMessageAsync(string sessionId, SendMessageRequest req, CancellationToken ct = default);

    Task<List<SessionDto>> ListSessionsAsync(CancellationToken ct = default);

    Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}
