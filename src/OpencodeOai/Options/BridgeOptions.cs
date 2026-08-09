namespace OpencodeOai.Options;

/// <summary>Bridge-level configuration bound from environment variables.</summary>
public sealed class BridgeOptions
{
    public const string SectionName = "Bridge";

    public int Port { get; set; } = 5000;
    public string DefaultModel { get; set; } = "gpt-4o";
    public string DefaultProviderId { get; set; } = "github-copilot";
    public string? ApiKey { get; set; }
    public int HeartbeatMs { get; set; } = 15_000;
    public int SessionTtlHours { get; set; } = 2;
    public int CleanupIntervalMs { get; set; } = 3_600_000;
    public int IdempotencyTtlHours { get; set; } = 24;
    public bool DevContainer { get; set; }
}
