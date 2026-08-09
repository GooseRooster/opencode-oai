using System.Text;
using System.Text.Json;
using OpencodeOai.Openai;

namespace OpencodeOai.Streaming;

/// <summary>
/// Writes buffered-parity SSE frames to the response body.
/// Emits an opening heartbeat loop while the upstream call is in flight,
/// then a role delta, a single content delta, a final finish_reason chunk,
/// and [DONE].
/// </summary>
internal sealed class SseWriter
{
    private readonly HttpResponse _response;
    private readonly TimeSpan _heartbeat;

    public SseWriter(HttpResponse response, int heartbeatMs)
    {
        _response = response;
        _heartbeat = TimeSpan.FromMilliseconds(heartbeatMs);
    }

    public async Task PrepareAsync(CancellationToken ct)
    {
        _response.StatusCode = StatusCodes.Status200OK;
        _response.Headers.ContentType = "text/event-stream";
        _response.Headers.CacheControl = "no-cache";
        _response.Headers.Connection = "keep-alive";
        await _response.Body.FlushAsync(ct);
    }

    public CancellationTokenSource StartHeartbeat(CancellationToken linkedTo)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(linkedTo);
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(_heartbeat, cts.Token);
                    await WriteRawAsync(": heartbeat\n\n", cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* swallow — response probably closed */ }
        }, cts.Token);
        return cts;
    }

    public Task WriteChunkAsync(ChatChunk chunk, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(chunk, OpenaiJsonContext.Default.ChatChunk);
        return WriteRawAsync($"data: {json}\n\n", ct);
    }

    public Task WriteErrorAsync(string message, CancellationToken ct)
    {
        var payload = new ErrorResponse { Error = new ErrorBody { Message = message, Type = "bridge_error" } };
        var json = JsonSerializer.Serialize(payload, OpenaiJsonContext.Default.ErrorResponse);
        return WriteRawAsync($"data: {json}\n\n", ct);
    }

    public Task WriteDoneAsync(CancellationToken ct) => WriteRawAsync("data: [DONE]\n\n", ct);

    private async Task WriteRawAsync(string frame, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(frame);
        await _response.Body.WriteAsync(bytes, ct);
        await _response.Body.FlushAsync(ct);
    }
}
