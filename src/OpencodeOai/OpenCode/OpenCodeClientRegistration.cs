using Microsoft.Extensions.Http.Resilience;
using OpencodeOai.OpenCode;
using OpencodeOai.Options;
using Polly;
using Polly.Timeout;

namespace OpencodeOai;

internal static class OpenCodeClientRegistration
{
    public static IServiceCollection AddOpenCodeClient(this IServiceCollection services)
    {
        services.AddHttpClient<IOpenCodeClient, OpenCodeClient>()
            .AddResilienceHandler("opencode", (builder, ctx) =>
            {
                var opts = ctx.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenCodeOptions>>().Value;

                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = opts.RetryCount,
                    Delay = TimeSpan.FromMilliseconds(opts.RetryDelayMs),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                        .HandleResult(r => (int)r.StatusCode >= 500)
                });

                builder.AddTimeout(TimeSpan.FromMilliseconds(opts.TimeoutMs));
            });

        return services;
    }
}
