using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using OpencodeOai.OpenCode.Models;
using Xunit;

namespace OpencodeOai.Tests;

public class EndpointsIntegrationTests
{
    [Fact]
    public async Task Health_returns_ok_even_when_upstream_unreachable()
    {
        using var factory = new BridgeFactory { ApiKey = null };
        factory.ClientMock
            .Setup(c => c.GetHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var res = await factory.CreateClient().GetAsync("/health");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("\"connected\":false");
    }

    [Fact]
    public async Task Models_returns_provider_models()
    {
        using var factory = new BridgeFactory { ApiKey = null };
        factory.ClientMock
            .Setup(c => c.GetProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BridgeFactory.SampleProviders());

        var res = await factory.CreateClient().GetAsync("/v1/models");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("github-copilot/gpt-4o");
    }

    [Fact]
    public async Task Models_falls_back_when_upstream_fails()
    {
        using var factory = new BridgeFactory { ApiKey = null };
        factory.ClientMock
            .Setup(c => c.GetProvidersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("nope"));

        var res = await factory.CreateClient().GetAsync("/v1/models");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("github-copilot/gpt-4o");
    }

    [Fact]
    public async Task Chat_completions_non_streaming_returns_openai_shape()
    {
        using var factory = new BridgeFactory { ApiKey = null };
        factory.ClientMock
            .Setup(c => c.CreateSessionAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionDto { Id = "sess-1" });
        factory.ClientMock
            .Setup(c => c.SendMessageAsync("sess-1", It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageResponse
            {
                Parts = new() { new PartDto { Type = "text", Text = "hi back" } },
                Info = new MessageInfo { Tokens = new TokenUsage { Input = 3, Output = 2, Total = 5 } },
            });
        factory.ClientMock
            .Setup(c => c.DeleteSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var client = factory.CreateClient();
        var body = new StringContent(
            "{\"model\":\"gpt-4o\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
            Encoding.UTF8, "application/json");

        var res = await client.PostAsync("/v1/chat/completions", body);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("object").GetString().Should().Be("chat.completion");
        doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString().Should().Be("hi back");
        doc.RootElement.GetProperty("choices")[0].GetProperty("finish_reason").GetString().Should().Be("stop");
        doc.RootElement.GetProperty("usage").GetProperty("total_tokens").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Chat_completions_streams_sse_frames()
    {
        using var factory = new BridgeFactory { ApiKey = null };
        factory.ClientMock
            .Setup(c => c.CreateSessionAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionDto { Id = "sess-1" });
        factory.ClientMock
            .Setup(c => c.SendMessageAsync("sess-1", It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageResponse
            {
                Parts = new() { new PartDto { Type = "text", Text = "stream ok" } },
            });
        factory.ClientMock
            .Setup(c => c.DeleteSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var client = factory.CreateClient();
        var body = new StringContent(
            "{\"model\":\"gpt-4o\",\"stream\":true,\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}]}",
            Encoding.UTF8, "application/json");

        var res = await client.PostAsync("/v1/chat/completions", body);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var text = await res.Content.ReadAsStringAsync();
        text.Should().Contain("data: ");
        text.Should().Contain("stream ok");
        text.Should().Contain("\"finish_reason\":\"stop\"");
        text.Should().EndWith("data: [DONE]\n\n");
    }

    [Fact]
    public async Task Chat_completions_rejects_empty_messages()
    {
        using var factory = new BridgeFactory { ApiKey = null };

        var client = factory.CreateClient();
        var body = new StringContent("{\"messages\":[]}", Encoding.UTF8, "application/json");

        var res = await client.PostAsync("/v1/chat/completions", body);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await res.Content.ReadAsStringAsync();
        text.Should().Contain("invalid_request_error");
    }

    [Fact]
    public async Task OpenApi_spec_is_served()
    {
        using var factory = new BridgeFactory { ApiKey = null };

        var res = await factory.CreateClient().GetAsync("/openapi.json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.");
        doc.RootElement.GetProperty("paths").TryGetProperty("/v1/chat/completions", out _).Should().BeTrue();
    }
}
