using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpencodeOai.Auth;
using OpencodeOai.Bridge;
using OpencodeOai.OpenCode;
using OpencodeOai.Openai;

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

        var reqStart = DateTimeOffset.UtcNow;

        try
        {
            var result = await service.CompleteAsync(body, ct);

            logger.LogInformation("chat completion ok in {Ms}ms tokens={Tokens} chars={Chars}",
                (DateTimeOffset.UtcNow - reqStart).TotalMilliseconds,
                result.TotalTokens,
                result.Text.Length);

            var response = new ChatCompletionResponse
            {
                Id = $"chatcmpl-{result.SessionId}",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = result.ModelId,
                Choices =
                {
                    new ChatChoice
                    {
                        Index = 0,
                        Message = new ChatChoiceMessage { Content = result.Text },
                        FinishReason = "stop",
                    },
                },
                Usage = new Usage
                {
                    PromptTokens = result.PromptTokens,
                    CompletionTokens = result.CompletionTokens,
                    TotalTokens = result.TotalTokens,
                },
            };

            return Results.Json(response, OpenaiJsonContext.Default.ChatCompletionResponse);
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
}
