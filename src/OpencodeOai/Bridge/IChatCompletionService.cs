using OpencodeOai.Openai;

namespace OpencodeOai.Bridge;

public interface IChatCompletionService
{
    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct);
}

public sealed record ChatCompletionResult(
    string SessionId,
    string ProviderId,
    string ModelId,
    string Text,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);
