using OpencodeOai.Options;

namespace OpencodeOai.Configuration;

/// <summary>
/// Maps environment variables onto <see cref="BridgeOptions"/> and <see cref="OpenCodeOptions"/>.
///
/// Naming scheme:
///   <c>OPENCODE_OAI_*</c> — bridge-side settings (this service's own config)
///   <c>OPENCODE_*</c>     — upstream connection settings (talking to the OpenCode server)
/// </summary>
internal static class EnvConfiguration
{
    public static void Apply(IConfigurationBuilder builder)
    {
        var env = Environment.GetEnvironmentVariables();
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);

        // ── Bridge-side (OPENCODE_OAI_*) ─────────────────────────────────────
        Map(env, overrides, $"{BridgeOptions.SectionName}:Port",                "OPENCODE_OAI_PORT");
        Map(env, overrides, $"{BridgeOptions.SectionName}:DefaultModel",        "OPENCODE_OAI_DEFAULT_MODEL");
        Map(env, overrides, $"{BridgeOptions.SectionName}:DefaultProviderId",   "OPENCODE_OAI_DEFAULT_PROVIDER");
        Map(env, overrides, $"{BridgeOptions.SectionName}:ApiKey",              "OPENCODE_OAI_API_KEY");
        Map(env, overrides, $"{BridgeOptions.SectionName}:HeartbeatMs",         "OPENCODE_OAI_HEARTBEAT_MS");
        Map(env, overrides, $"{BridgeOptions.SectionName}:SessionTtlHours",     "OPENCODE_OAI_SESSION_TTL_HOURS");
        Map(env, overrides, $"{BridgeOptions.SectionName}:CleanupIntervalMs",   "OPENCODE_OAI_CLEANUP_INTERVAL_MS");
        Map(env, overrides, $"{BridgeOptions.SectionName}:IdempotencyTtlHours", "OPENCODE_OAI_IDEMPOTENCY_TTL_HOURS");
        MapBool(env, overrides, $"{BridgeOptions.SectionName}:DevContainer",    "OPENCODE_OAI_DEVCONTAINER");
        MapBool(env, overrides, $"{BridgeOptions.SectionName}:LogPrompts",      "OPENCODE_OAI_LOG_PROMPTS");
        Map(env, overrides, $"{BridgeOptions.SectionName}:LogPreviewChars",     "OPENCODE_OAI_LOG_PREVIEW_CHARS");

        // Log level feeds Microsoft.Extensions.Logging directly.
        Map(env, overrides, "Logging:LogLevel:Default",                         "OPENCODE_OAI_LOG_LEVEL");

        // ── Upstream OpenCode connection (OPENCODE_*) ─────────────────────────
        Map(env, overrides, $"{OpenCodeOptions.SectionName}:Url",          "OPENCODE_URL");
        Map(env, overrides, $"{OpenCodeOptions.SectionName}:Username",     "OPENCODE_USERNAME");
        Map(env, overrides, $"{OpenCodeOptions.SectionName}:Password",     "OPENCODE_PASSWORD");
        Map(env, overrides, $"{OpenCodeOptions.SectionName}:TimeoutMs",    "OPENCODE_TIMEOUT_MS");
        Map(env, overrides, $"{OpenCodeOptions.SectionName}:RetryCount",   "OPENCODE_RETRY_COUNT");
        Map(env, overrides, $"{OpenCodeOptions.SectionName}:RetryDelayMs", "OPENCODE_RETRY_DELAY_MS");

        if (overrides.Count > 0)
        {
            builder.AddInMemoryCollection(overrides);
        }
    }

    private static void Map(System.Collections.IDictionary env, IDictionary<string, string?> sink, string target, string key)
    {
        var value = env[key] as string;
        if (!string.IsNullOrEmpty(value))
        {
            sink[target] = value;
        }
    }

    private static void MapBool(System.Collections.IDictionary env, IDictionary<string, string?> sink, string target, string key)
    {
        var value = env[key] as string;
        if (string.IsNullOrEmpty(value)) return;
        var truthy = value.Equals("true", StringComparison.OrdinalIgnoreCase)
                  || value.Equals("1",    StringComparison.Ordinal)
                  || value.Equals("yes",  StringComparison.OrdinalIgnoreCase);
        sink[target] = truthy ? "true" : "false";
    }
}
