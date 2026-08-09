using FluentAssertions;
using Microsoft.Extensions.Options;
using OpencodeOai.Bridge;
using OpencodeOai.Options;
using Xunit;

namespace OpencodeOai.Tests;

public class IdempotencyStoreTests
{
    private static IIdempotencyStore NewStore(int ttlHours = 24) =>
        new MemoryIdempotencyStore(Microsoft.Extensions.Options.Options.Create(new BridgeOptions { IdempotencyTtlHours = ttlHours }));

    [Fact]
    public async Task Caches_result_by_key()
    {
        var store = NewStore();
        var calls = 0;

        var first  = await store.GetOrAddAsync("k", _ => Task.FromResult(Sample(++calls)), CancellationToken.None);
        var second = await store.GetOrAddAsync("k", _ => Task.FromResult(Sample(++calls)), CancellationToken.None);

        calls.Should().Be(1);
        second.SessionId.Should().Be(first.SessionId);
    }

    [Fact]
    public async Task Coalesces_concurrent_same_key_requests()
    {
        var store = NewStore();
        var calls = 0;
        var gate  = new TaskCompletionSource<ChatCompletionResult>();

        async Task<ChatCompletionResult> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            return await gate.Task;
        }

        var t1 = store.GetOrAddAsync("k", Factory, CancellationToken.None);
        var t2 = store.GetOrAddAsync("k", Factory, CancellationToken.None);

        gate.SetResult(Sample(1));

        var (r1, r2) = (await t1, await t2);

        calls.Should().Be(1);
        r1.Should().BeSameAs(r2);
    }

    [Fact]
    public async Task Evicts_faulted_entries_so_retry_can_succeed()
    {
        var store = NewStore();
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetOrAddAsync("k", _ =>
            {
                attempts++;
                return Task.FromException<ChatCompletionResult>(new InvalidOperationException("boom"));
            }, CancellationToken.None));

        var ok = await store.GetOrAddAsync("k", _ =>
        {
            attempts++;
            return Task.FromResult(Sample(1));
        }, CancellationToken.None);

        attempts.Should().Be(2);
        ok.SessionId.Should().Be("s1");
    }

    private static ChatCompletionResult Sample(int i) =>
        new($"s{i}", "p", "m", "text", null, 0, 0, 0);
}
