using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OpencodeOai.Bridge;
using OpencodeOai.OpenCode;
using OpencodeOai.OpenCode.Models;
using OpencodeOai.Openai;
using OpencodeOai.Options;
using Xunit;

namespace OpencodeOai.Tests;

public class ChatCompletionServiceTests
{
    private static ChatCompletionRequest Req(string? model, params (string role, string content)[] messages) => new()
    {
        Model = model,
        Messages = messages.Select(m => new ChatMessage
        {
            Role = m.role,
            Content = JsonSerializer.SerializeToElement(m.content),
        }).ToList(),
    };

    private static (ChatCompletionService svc, Mock<IOpenCodeClient> client) Build(BridgeOptions? opts = null)
    {
        var mock = new Mock<IOpenCodeClient>(MockBehavior.Strict);
        var options = Microsoft.Extensions.Options.Options.Create(opts ?? new BridgeOptions
        {
            DefaultModel = "gpt-4o",
            DefaultProviderId = "github-copilot",
        });
        return (new ChatCompletionService(mock.Object, options, NullLogger<ChatCompletionService>.Instance), mock);
    }

    [Fact]
    public async Task Throws_when_messages_empty()
    {
        var (svc, _) = Build();
        var request = new ChatCompletionRequest { Messages = new() };

        await Assert.ThrowsAsync<ArgumentException>(() => svc.CompleteAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Splits_provider_from_model_when_slashed()
    {
        var (svc, client) = Build();

        SendMessageRequest? captured = null;
        client.Setup(c => c.CreateSessionAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SessionDto { Id = "s1" });
        client.Setup(c => c.SendMessageAsync("s1", It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
              .Callback<string, SendMessageRequest, CancellationToken>((_, r, _) => captured = r)
              .ReturnsAsync(new MessageResponse { Parts = new() { new PartDto { Type = "text", Text = "ok" } } });
        client.Setup(c => c.DeleteSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await svc.CompleteAsync(Req("anthropic/claude-3-opus", ("user", "hi")), CancellationToken.None);

        captured!.Model.ProviderId.Should().Be("anthropic");
        captured.Model.ModelId.Should().Be("claude-3-opus");
    }

    [Fact]
    public async Task Uses_default_provider_when_model_bare()
    {
        var (svc, client) = Build();

        SendMessageRequest? captured = null;
        client.Setup(c => c.CreateSessionAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SessionDto { Id = "s1" });
        client.Setup(c => c.SendMessageAsync("s1", It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
              .Callback<string, SendMessageRequest, CancellationToken>((_, r, _) => captured = r)
              .ReturnsAsync(new MessageResponse { Parts = new() { new PartDto { Type = "text", Text = "ok" } } });
        client.Setup(c => c.DeleteSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await svc.CompleteAsync(Req("gpt-4o", ("user", "hi")), CancellationToken.None);

        captured!.Model.ProviderId.Should().Be("github-copilot");
        captured.Model.ModelId.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task Joins_all_text_segments()
    {
        var (svc, client) = Build();

        client.Setup(c => c.CreateSessionAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SessionDto { Id = "s1" });
        client.Setup(c => c.SendMessageAsync("s1", It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new MessageResponse
              {
                  Parts = new()
                  {
                      new PartDto { Type = "text", Text = "first" },
                      new PartDto { Type = "text", Text = "second" },
                  },
                  Info = new MessageInfo { Tokens = new TokenUsage { Input = 10, Output = 5, Total = 15 } },
              });
        client.Setup(c => c.DeleteSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await svc.CompleteAsync(Req(null, ("user", "hi")), CancellationToken.None);

        result.Text.Should().Be("first\n\nsecond");
        result.PromptTokens.Should().Be(10);
        result.CompletionTokens.Should().Be(5);
        result.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task Deletes_session_after_success()
    {
        var (svc, client) = Build();

        client.Setup(c => c.CreateSessionAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SessionDto { Id = "s1" });
        client.Setup(c => c.SendMessageAsync("s1", It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new MessageResponse { Parts = new() { new PartDto { Type = "text", Text = "ok" } } });
        client.Setup(c => c.DeleteSessionAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(true).Verifiable();

        await svc.CompleteAsync(Req(null, ("user", "hi")), CancellationToken.None);

        await WaitForBackgroundDeleteAsync(client);
        client.Verify(c => c.DeleteSessionAsync("s1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deletes_session_after_failure()
    {
        var (svc, client) = Build();

        client.Setup(c => c.CreateSessionAsync(It.IsAny<CreateSessionRequest>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new SessionDto { Id = "s1" });
        client.Setup(c => c.SendMessageAsync("s1", It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new OpenCodeException("upstream boom", 500));
        client.Setup(c => c.DeleteSessionAsync("s1", It.IsAny<CancellationToken>())).ReturnsAsync(true).Verifiable();

        await Assert.ThrowsAsync<OpenCodeException>(() => svc.CompleteAsync(Req(null, ("user", "hi")), CancellationToken.None));

        await WaitForBackgroundDeleteAsync(client);
        client.Verify(c => c.DeleteSessionAsync("s1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task WaitForBackgroundDeleteAsync(Mock<IOpenCodeClient> client)
    {
        for (var i = 0; i < 50; i++)
        {
            if (client.Invocations.Any(inv => inv.Method.Name == nameof(IOpenCodeClient.DeleteSessionAsync))) return;
            await Task.Delay(20);
        }
    }
}
