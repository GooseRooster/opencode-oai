using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpencodeOai.Auth;
using OpencodeOai.Bridge;
using OpencodeOai.Logging;
using OpencodeOai.OpenCode;
using OpencodeOai.Openai;
using OpencodeOai.Options;
using OpencodeOai.Streaming;

namespace OpencodeOai.Endpoints;

internal sealed class ChatCompletionsEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/chat/completions", HandleAsync)
           .RequireAuthorization(ApiKeyAuthHandler.SchemeName);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext ctx,
        [FromBody] ChatCompletionRequest? body,
        IChatCompletionService service,
        IIdempotencyStore idempotency,
        IOptions<BridgeOptions> bridgeOpts,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("ChatCompletions");
        var bridge = bridgeOpts.Value;

        if (body is null || body.Messages is null || body.Messages.Count == 0)
        {
            logger.LogWarning("chat request {RequestId} rejected — empty `messages`", ctx.TraceIdentifier);
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = "`messages` must be a non-empty array", Type = "invalid_request_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var requestId = ctx.TraceIdentifier;

        logger.LogInformation("chat request {RequestId} model={Model} messages={Messages} stream={Stream}",
            requestId, body.Model ?? "(default)", body.Messages.Count, body.Stream);

        if (bridge.LogPrompts)
        {
            logger.LogInformation("chat prompt {RequestId} {Prompt}",
                requestId, LogPreview.Messages(body.Messages, bridge.LogPreviewChars));
        }

        Func<CancellationToken, Task<ChatCompletionResult>> invoke = c => service.CompleteAsync(body, c);

        if (ctx.Request.Headers.TryGetValue("Idempotency-Key", out var idKey) && !string.IsNullOrEmpty(idKey))
        {
            var authKey = ctx.Request.Headers.Authorization.ToString();
            var key = $"{authKey}::{idKey}";
            invoke = c => idempotency.GetOrAddAsync(key, t => service.CompleteAsync(body, t), c);
        }

        if (body.Stream)
        {
            await HandleStreamingAsync(ctx, invoke, bridge, logger, requestId, ct);
            return Results.Empty;
        }

        return await HandleBufferedAsync(invoke, bridge, logger, requestId, ct);
    }

    private static async Task<IResult> HandleBufferedAsync(
        Func<CancellationToken, Task<ChatCompletionResult>> invoke,
        BridgeOptions bridge,
        ILogger logger,
        string requestId,
        CancellationToken ct)
    {
        var start = DateTimeOffset.UtcNow;
        try
        {
            var result = await invoke(ct);
            LogCompletion(logger, bridge, requestId, result, stream: false, start);

            return Results.Json(BuildResponse(result), OpenaiJsonContext.Default.ChatCompletionResponse);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("client cancelled {RequestId} after {Ms}ms",
                requestId, Elapsed(start));
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("chat request {RequestId} rejected — {Reason}", requestId, ex.Message);
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = ex.Message, Type = "invalid_request_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (OpenCodeException ex)
        {
            logger.LogError(ex, "opencode upstream error on {RequestId}", requestId);
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = ex.Message, Type = "bridge_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "bridge failure on {RequestId}", requestId);
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = ex.Message, Type = "bridge_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task HandleStreamingAsync(
        HttpContext ctx,
        Func<CancellationToken, Task<ChatCompletionResult>> invoke,
        BridgeOptions bridge,
        ILogger logger,
        string requestId,
        CancellationToken ct)
    {
        var sse = new SseWriter(ctx.Response, bridge.HeartbeatMs);
        await sse.PrepareAsync(ct);

        var heartbeat = sse.StartHeartbeat(ct);
        ChatCompletionResult? result = null;
        Exception? failure = null;
        var start = DateTimeOffset.UtcNow;
        var cancelled = false;

        try
        {
            result = await invoke(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            heartbeat.Cancel();
        }

        if (cancelled)
        {
            logger.LogDebug("client cancelled streaming {RequestId} after {Ms}ms",
                requestId, Elapsed(start));
            return;
        }

        if (failure is not null)
        {
            logger.LogError(failure, "bridge streaming failure on {RequestId}", requestId);
            try
            {
                await sse.WriteErrorAsync(failure.Message, ct);
                await sse.WriteDoneAsync(ct);
            }
            catch (OperationCanceledException) { }
            return;
        }

        var r = result!;
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cmplId = $"chatcmpl-{r.SessionId}";

        try
        {
            await sse.WriteChunkAsync(new ChatChunk
            {
                Id = cmplId, Created = created, Model = r.ModelId,
                Choices = { new ChatChunkChoice { Index = 0, Delta = new ChatChunkDelta { Role = "assistant", Content = "" }, FinishReason = null } },
            }, ct);

            if (!string.IsNullOrEmpty(r.Reasoning))
            {
                await sse.WriteChunkAsync(new ChatChunk
                {
                    Id = cmplId, Created = created, Model = r.ModelId,
                    Choices = { new ChatChunkChoice { Index = 0, Delta = new ChatChunkDelta { ReasoningContent = r.Reasoning }, FinishReason = null } },
                }, ct);
            }

            await sse.WriteChunkAsync(new ChatChunk
            {
                Id = cmplId, Created = created, Model = r.ModelId,
                Choices = { new ChatChunkChoice { Index = 0, Delta = new ChatChunkDelta { Content = r.Text }, FinishReason = null } },
            }, ct);

            await sse.WriteChunkAsync(new ChatChunk
            {
                Id = cmplId, Created = created, Model = r.ModelId,
                Choices = { new ChatChunkChoice { Index = 0, Delta = new ChatChunkDelta(), FinishReason = "stop" } },
                Usage = new Usage { PromptTokens = r.PromptTokens, CompletionTokens = r.CompletionTokens, TotalTokens = r.TotalTokens },
            }, ct);

            await sse.WriteDoneAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("client cancelled streaming {RequestId} mid-emit after {Ms}ms",
                requestId, Elapsed(start));
            return;
        }

        LogCompletion(logger, bridge, requestId, r, stream: true, start);
    }

    /// <summary>
    /// One metadata line per successful completion, plus opt-in content previews
    /// when <c>OPENCODE_OAI_LOG_PROMPTS</c> is set.
    /// </summary>
    private static void LogCompletion(
        ILogger logger,
        BridgeOptions bridge,
        string requestId,
        ChatCompletionResult r,
        bool stream,
        DateTimeOffset start)
    {
        logger.LogInformation(
            "chat completion ok {RequestId} model={Provider}/{Model} stream={Stream} in {Ms}ms tokens=in:{Prompt} out:{Completion} total:{Total} chars={Chars}",
            requestId, r.ProviderId, r.ModelId, stream, Elapsed(start),
            r.PromptTokens, r.CompletionTokens, r.TotalTokens, r.Text.Length);

        if (!bridge.LogPrompts) return;

        if (!string.IsNullOrEmpty(r.Reasoning))
        {
            logger.LogInformation("chat reasoning {RequestId} {Reasoning}",
                requestId, LogPreview.Truncate(r.Reasoning, bridge.LogPreviewChars));
        }

        logger.LogInformation("chat output {RequestId} {Output}",
            requestId, LogPreview.Truncate(r.Text, bridge.LogPreviewChars));
    }

    /// <summary>Whole milliseconds since <paramref name="start"/>. Sub-ms precision is noise here.</summary>
    private static long Elapsed(DateTimeOffset start) => (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds;

    private static ChatCompletionResponse BuildResponse(ChatCompletionResult r) => new()
    {
        Id = $"chatcmpl-{r.SessionId}",
        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Model = r.ModelId,
        Choices =
        {
            new ChatChoice
            {
                Index = 0,
                Message = new ChatChoiceMessage { Content = r.Text, ReasoningContent = r.Reasoning },
                FinishReason = "stop",
            },
        },
        Usage = new Usage { PromptTokens = r.PromptTokens, CompletionTokens = r.CompletionTokens, TotalTokens = r.TotalTokens },
    };
}
