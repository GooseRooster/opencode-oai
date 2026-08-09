using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using OpencodeOai.Options;

namespace OpencodeOai.Bridge;

internal sealed class MemoryIdempotencyStore : IIdempotencyStore
{
    private sealed record Entry(Task<ChatCompletionResult> Task, DateTimeOffset Expires);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;

    public MemoryIdempotencyStore(IOptions<BridgeOptions> options)
    {
        _ttl = TimeSpan.FromHours(options.Value.IdempotencyTtlHours);
    }

    public Task<ChatCompletionResult> GetOrAddAsync(
        string key,
        Func<CancellationToken, Task<ChatCompletionResult>> factory,
        CancellationToken ct)
    {
        Prune();

        var entry = _entries.GetOrAdd(key, _ =>
        {
            var task = factory(ct);
            return new Entry(task, DateTimeOffset.UtcNow.Add(_ttl));
        });

        // If the cached task faulted, evict so a retry can rebuild.
        if (entry.Task.IsFaulted || entry.Task.IsCanceled)
        {
            _entries.TryRemove(key, out _);
            return GetOrAddAsync(key, factory, ct);
        }

        return entry.Task;
    }

    private void Prune()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _entries)
        {
            if (kv.Value.Expires < now)
            {
                _entries.TryRemove(kv.Key, out _);
            }
        }
    }
}
