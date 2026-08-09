using Microsoft.Extensions.Options;
using OpencodeOai.OpenCode;
using OpencodeOai.Options;

namespace OpencodeOai.Background;

/// <summary>
/// Periodically reaps orphaned bridge-created OpenCode sessions.
/// Backstop for the per-request fire-and-forget delete in <see cref="Bridge.ChatCompletionService"/>.
/// </summary>
internal sealed class SessionCleanupService : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<BridgeOptions> _bridge;
    private readonly ILogger<SessionCleanupService> _logger;

    public SessionCleanupService(
        IServiceProvider services,
        IOptionsMonitor<BridgeOptions> bridge,
        ILogger<SessionCleanupService> logger)
    {
        _services = services;
        _bridge = bridge;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await RunOnceAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var interval = TimeSpan.FromMilliseconds(_bridge.CurrentValue.CleanupIntervalMs);
                await Task.Delay(interval, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IOpenCodeClient>();
            var ttlHours = _bridge.CurrentValue.SessionTtlHours;
            var cutoffMs = DateTimeOffset.UtcNow.AddHours(-ttlHours).ToUnixTimeMilliseconds();
            var sessions = await client.ListSessionsAsync(ct);
            var deleted = 0;

            foreach (var s in sessions)
            {
                if (string.IsNullOrEmpty(s.Id)) continue;
                if (s.Title is null || !s.Title.StartsWith("bridge-", StringComparison.Ordinal)) continue;
                if ((s.Time?.Created ?? 0) >= cutoffMs) continue;

                if (await client.DeleteSessionAsync(s.Id, ct))
                {
                    deleted++;
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("session cleanup deleted {Deleted} sessions older than {Ttl}h", deleted, ttlHours);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "session cleanup failed");
        }
    }
}
