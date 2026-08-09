using System.Net;
using FluentAssertions;
using Moq;
using Xunit;

namespace OpencodeOai.Tests;

public class ApiKeyAuthTests
{
    [Fact]
    public async Task Rejects_missing_header_when_key_configured()
    {
        using var factory = new BridgeFactory { ApiKey = "secret" };
        var client = factory.CreateClient();

        var res = await client.GetAsync("/v1/models");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_invalid_key()
    {
        using var factory = new BridgeFactory { ApiKey = "secret" };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "nope");

        var res = await client.GetAsync("/v1/models");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Accepts_valid_bearer_token()
    {
        using var factory = new BridgeFactory { ApiKey = "secret" };
        factory.ClientMock
            .Setup(c => c.GetProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BridgeFactory.SampleProviders());

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "secret");

        var res = await client.GetAsync("/v1/models");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Allows_anonymous_when_key_env_is_empty()
    {
        using var factory = new BridgeFactory { ApiKey = null };
        factory.ClientMock
            .Setup(c => c.GetProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BridgeFactory.SampleProviders());

        var client = factory.CreateClient();

        var res = await client.GetAsync("/v1/models");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
