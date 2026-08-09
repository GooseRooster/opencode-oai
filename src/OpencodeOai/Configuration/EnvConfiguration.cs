using OpencodeOai.Options;

namespace OpencodeOai.Configuration;

/// <summary>
/// Maps the npm-bridge-compatible flat env var names onto
/// <see cref="BridgeOptions"/> and <see cref="OpenCodeOptions"/>.
/// </summary>
internal static class EnvConfiguration
{
    public static void Apply(IConfigurationBuilder builder)
    {
        var env = Environment.GetEnvironmentVariables();
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);

        Map(overrides, env, "PORT",                         $"{BridgeOptions.SectionName}:Port");
        Map(overrides, env, "DEFAULT_MODEL",                $"{BridgeOptions.SectionName}:DefaultModel");
        Map(overrides, env, "OPENCODE_PROVIDER_ID",         $"{BridgeOptions.SectionName}:DefaultProviderId");
        Map(overrides, env, "OPENCODE_PROXY_API_KEY",       $"{BridgeOptions.SectionName}:ApiKey");
        Map(overrides, env, "HEARTBEAT_MS",                 $"{BridgeOptions.SectionName}:HeartbeatMs");
        Map(overrides, env, "SESSION_TTL_HOURS",            $"{BridgeOptions.SectionName}:SessionTtlHours");
        Map(overrides, env, "CLEANUP_INTERVAL_MS",          $"{BridgeOptions.SectionName}:CleanupIntervalMs");
        Map(overrides, env, "IDEMPOTENCY_TTL_HOURS",        $"{BridgeOptions.SectionName}:IdempotencyTtlHours");
        MapBool(overrides, env, "DEVCONTAINER",             $"{BridgeOptions.SectionName}:DevContainer");

        Map(overrides, env, "OPENCODE_URL",                 $"{OpenCodeOptions.SectionName}:Url");
        Map(overrides, env, "OPENCODE_SERVER_USERNAME",     $"{OpenCodeOptions.SectionName}:Username");
        Map(overrides, env, "OPENCODE_SERVER_PASSWORD",     $"{OpenCodeOptions.SectionName}:Password");
        Map(overrides, env, "TIMEOUT_MS",                   $"{OpenCodeOptions.SectionName}:TimeoutMs");
        Map(overrides, env, "RETRY_COUNT",                  $"{OpenCodeOptions.SectionName}:RetryCount");
        Map(overrides, env, "RETRY_DELAY_MS",               $"{OpenCodeOptions.SectionName}:RetryDelayMs");

        if (overrides.Count > 0)
        {
            builder.AddInMemoryCollection(overrides);
        }
    }

    private static void Map(IDictionary<string, string?> sink, System.Collections.IDictionary env, string key, string target)
    {
        var value = env[key] as string;
        if (!string.IsNullOrEmpty(value))
        {
            sink[target] = value;
        }
    }

    private static void MapBool(IDictionary<string, string?> sink, System.Collections.IDictionary env, string key, string target)
    {
        var value = env[key] as string;
        if (string.IsNullOrEmpty(value)) return;
        var truthy = value.Equals("true", StringComparison.OrdinalIgnoreCase)
                  || value.Equals("1",    StringComparison.Ordinal)
                  || value.Equals("yes",  StringComparison.OrdinalIgnoreCase);
        sink[target] = truthy ? "true" : "false";
    }
}
