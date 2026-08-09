using OpencodeOai.Bridge;

namespace OpencodeOai.Bridge;

public interface IIdempotencyStore
{
    /// <summary>
    /// Returns an existing in-flight or cached completion for <paramref name="key"/>,
    /// or invokes <paramref name="factory"/> to produce and cache one.
    /// </summary>
    Task<ChatCompletionResult> GetOrAddAsync(
        string key,
        Func<CancellationToken, Task<ChatCompletionResult>> factory,
        CancellationToken ct);
}
