using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpencodeOai.Auth;
using OpencodeOai.Bridge;
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
        IOptions<BridgeOptions> bridgeOpts,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("ChatCompletions");

        if (body is null || body.Messages is null || body.Messages.Count == 0)
        {
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = "`messages` must be a non-empty array", Type = "invalid_request_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (body.Stream)
        {
            await HandleStreamingAsync(ctx, body, service, bridgeOpts.Value, logger, ct);
            return Results.Empty;
        }

        return await HandleBufferedAsync(body, service, logger, ct);
    }

    private static async Task<IResult> HandleBufferedAsync(
        ChatCompletionRequest body,
        IChatCompletionService service,
        ILogger logger,
        CancellationToken ct)
    {
        var start = DateTimeOffset.UtcNow;
        try
        {
            var result = await service.CompleteAsync(body, ct);
            logger.LogInformation("chat completion ok in {Ms}ms tokens={Tokens} chars={Chars}",
                (DateTimeOffset.UtcNow - start).TotalMilliseconds, result.TotalTokens, result.Text.Length);

            return Results.Json(BuildResponse(result), OpenaiJsonContext.Default.ChatCompletionResponse);
        }
        catch (ArgumentException ex)
        {
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = ex.Message, Type = "invalid_request_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (OpenCodeException ex)
        {
            logger.LogError(ex, "opencode upstream error");
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = ex.Message, Type = "bridge_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "bridge failure");
            return Results.Json(
                new ErrorResponse { Error = new ErrorBody { Message = ex.Message, Type = "bridge_error" } },
                OpenaiJsonContext.Default.ErrorResponse,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task HandleStreamingAsync(
        HttpContext ctx,
        ChatCompletionRequest body,
        IChatCompletionService service,
        BridgeOptions bridge,
        ILogger logger,
        CancellationToken ct)
    {
        var sse = new SseWriter(ctx.Response, bridge.HeartbeatMs);
        await sse.PrepareAsync(ct);

        var heartbeat = sse.StartHeartbeat(ct);
        ChatCompletionResult? result = null;
        Exception? failure = null;

        try
        {
            result = await service.CompleteAsync(body, ct);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            heartbeat.Cancel();
        }

        if (failure is not null)
        {
            logger.LogError(failure, "bridge streaming failure");
            await sse.WriteErrorAsync(failure.Message, ct);
            await sse.WriteDoneAsync(ct);
            return;
        }

        var r = result!;
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cmplId = $"chatcmpl-{r.SessionId}";

        await sse.WriteChunkAsync(new ChatChunk
        {
            Id = cmplId, Created = created, Model = r.ModelId,
            Choices = { new ChatChunkChoice { Index = 0, Delta = new ChatChunkDelta { Role = "assistant", Content = "" }, FinishReason = null } },
        }, ct);

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
                Message = new ChatChoiceMessage { Content = r.Text },
                FinishReason = "stop",
            },
        },
        Usage = new Usage { PromptTokens = r.PromptTokens, CompletionTokens = r.CompletionTokens, TotalTokens = r.TotalTokens },
    };
}
