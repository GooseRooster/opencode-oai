using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpencodeOai.OpenCode;
using OpencodeOai.OpenCode.Models;
using Xunit;

namespace OpencodeOai.Tests;

/// <summary>Shared factory that stubs the OpenCode client and lets tests set an API key.</summary>
public sealed class BridgeFactory : WebApplicationFactory<Program>
{
    public Mock<IOpenCodeClient> ClientMock { get; } = new(MockBehavior.Loose);
    public string? ApiKey { get; set; }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        if (ApiKey is not null)
        {
            Environment.SetEnvironmentVariable("OPENCODE_PROXY_API_KEY", ApiKey);
        }
        else
        {
            Environment.SetEnvironmentVariable("OPENCODE_PROXY_API_KEY", null);
        }

        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(IOpenCodeClient));
            services.Remove(descriptor);
            services.AddSingleton(ClientMock.Object);
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
