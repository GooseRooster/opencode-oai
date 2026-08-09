namespace OpencodeOai.Options;

/// <summary>OpenCode upstream configuration bound from environment variables.</summary>
public sealed class OpenCodeOptions
{
    public const string SectionName = "OpenCode";

    public string Url { get; set; } = "http://localhost:4096";
    public string Username { get; set; } = "opencode";
    public string? Password { get; set; }
    public int TimeoutMs { get; set; } = 600_000;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 2_000;
}
