using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpencodeOai.OpenCode;
using OpencodeOai.OpenCode.Models;
using OpencodeOai.Options;

namespace OpencodeOai.Tests;

/// <summary>Test host with a stubbed IOpenCodeClient and overridable BridgeOptions.</summary>
public sealed class BridgeFactory : WebApplicationFactory<Program>
{
    public Mock<IOpenCodeClient> ClientMock { get; } = new(MockBehavior.Loose);
    public string? ApiKey { get; set; }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(IOpenCodeClient));
            services.Remove(descriptor);
            services.AddSingleton(ClientMock.Object);

            services.Configure<BridgeOptions>(o =>
            {
                o.ApiKey = ApiKey;
                o.DefaultModel = "gpt-4o";
                o.DefaultProviderId = "github-copilot";
                o.HeartbeatMs = 60_000;
            });
        });
    }

    public static ProvidersResponse SampleProviders() => new()
    {
        Connected = new() { "github-copilot" },
        All = new()
        {
            new ProviderDto
            {
                Id = "github-copilot",
                Models = new()
                {
                    ["gpt-4o"] = new ProviderModelDto { Id = "gpt-4o" },
                },
            },
        },
    };
}
