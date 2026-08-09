using Microsoft.Extensions.Options;
using OpencodeOai.OpenCode;
using OpencodeOai.OpenCode.Models;
using OpencodeOai.Openai;
using OpencodeOai.Options;

namespace OpencodeOai.Bridge;

internal sealed class ChatCompletionService : IChatCompletionService
{
    private readonly IOpenCodeClient _client;
    private readonly IOptions<BridgeOptions> _bridge;
    private readonly ILogger<ChatCompletionService> _logger;

    public ChatCompletionService(
        IOpenCodeClient client,
        IOptions<BridgeOptions> bridge,
        ILogger<ChatCompletionService> logger)
    {
        _client = client;
        _bridge = bridge;
        _logger = logger;
    }

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct)
    {
        if (request.Messages is null || request.Messages.Count == 0)
        {
            throw new ArgumentException("`messages` must be a non-empty array", nameof(request));
        }

        var bridge = _bridge.Value;
        var (providerId, modelId) = SplitModel(request.Model, bridge);

        if (request.Tools is not null || request.ToolChoice is not null)
        {
            _logger.LogInformation("tool-related fields dropped — unsupported in this bridge");
        }

        if (request.ReasoningEffort is not null)
        {
            _logger.LogInformation("reasoning_effort dropped — OpenCode has no per-request knob; select a reasoning model via `model` instead");
        }

        var built = PartsBuilder.Build(request.Messages);
        if (built.HasImage) _logger.LogInformation("multimodal — images detected");
        if (built.DroppedToolFields) _logger.LogInformation("tool-role messages dropped — unsupported in this bridge");

        var reqId = $"req_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var session = await _client.CreateSessionAsync(new CreateSessionRequest { Title = $"bridge-{reqId}" }, ct);
        var sessionId = session.Id;

        try
        {
            var result = await _client.SendMessageAsync(sessionId, new SendMessageRequest
            {
                Model = new ModelRef { ProviderId = providerId, ModelId = modelId },
                System = built.System,
                Parts = built.Parts,
            }, ct);

            var (text, reasoning) = ExtractContent(result);
            var tokens = result.Info?.Tokens;

            return new ChatCompletionResult(
                SessionId: sessionId,
                ProviderId: providerId,
                ModelId: modelId,
                Text: text,
                Reasoning: reasoning,
                PromptTokens: tokens?.Input ?? 0,
                CompletionTokens: tokens?.Output ?? 0,
                TotalTokens: tokens?.Total ?? 0);
        }
        finally
        {
            // Fire-and-forget cleanup. Backstopped by SessionCleanupService.
            _ = Task.Run(async () =>
            {
                try
                {
                    await _client.DeleteSessionAsync(sessionId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "background session delete failed for {SessionId}", sessionId);
                }
            }, CancellationToken.None);
        }
    }

    private static (string ProviderId, string ModelId) SplitModel(string? incoming, BridgeOptions bridge)
    {
        var model = string.IsNullOrEmpty(incoming) ? bridge.DefaultModel : incoming;
        var slash = model.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0) return (bridge.DefaultProviderId, model);
        return (model[..slash], model[(slash + 1)..]);
    }

    private static (string Text, string? Reasoning) ExtractContent(MessageResponse result)
    {
        if (result.Parts is null || result.Parts.Count == 0) return ("", null);

        var texts = new List<string>();
        var reasonings = new List<string>();

        foreach (var p in result.Parts)
        {
            if (p.Type == "text" && !string.IsNullOrWhiteSpace(p.Text))
            {
                texts.Add(p.Text.Trim());
            }
            else if (p.Type == "reasoning" && !string.IsNullOrWhiteSpace(p.Text))
            {
                reasonings.Add(p.Text.Trim());
            }
        }

        var text = string.Join("\n\n", texts);
        var reasoning = reasonings.Count > 0 ? string.Join("\n\n", reasonings) : null;
        return (text, reasoning);
    }
}
