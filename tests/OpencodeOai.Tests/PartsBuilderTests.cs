using System.Text.Json;
using FluentAssertions;
using OpencodeOai.Bridge;
using OpencodeOai.Openai;
using Xunit;

namespace OpencodeOai.Tests;

public class PartsBuilderTests
{
    private static ChatMessage Msg(string role, string content) => new()
    {
        Role = role,
        Content = JsonSerializer.SerializeToElement(content),
    };

    private static ChatMessage Msg(string role, object content) => new()
    {
        Role = role,
        Content = JsonSerializer.SerializeToElement(content),
    };

    [Fact]
    public void Builds_text_parts_for_string_content()
    {
        var messages = new List<ChatMessage>
        {
            Msg("user", "hello"),
        };

        var result = PartsBuilder.Build(messages);

        result.Parts.Should().HaveCount(1);
        result.Parts[0].Type.Should().Be("text");
        result.Parts[0].Text.Should().Contain("[USER]").And.Contain("hello");
    }

    [Fact]
    public void Extracts_system_message_as_system_part()
    {
        var messages = new List<ChatMessage>
        {
            Msg("system", "You are a helpful assistant."),
            Msg("user", "hi"),
        };

        var result = PartsBuilder.Build(messages);

        result.Parts.Should().Contain(p => p.Type == "system" && p.Text == "You are a helpful assistant.");
        result.Parts.Should().NotContain(p => p.Text != null && p.Text.Contains("[SYSTEM]"));
    }

    [Fact]
    public void Handles_multimodal_data_uri_image()
    {
        var messages = new List<ChatMessage>
        {
            Msg("user", new object[]
            {
                new { type = "text", text = "look" },
                new { type = "image_url", image_url = new { url = "data:image/png;base64,AAAA" } },
            }),
        };

        var result = PartsBuilder.Build(messages);

        result.HasImage.Should().BeTrue();
        var image = result.Parts.Single(p => p.Type == "image");
        image.Source!.Type.Should().Be("base64");
        image.Source.MediaType.Should().Be("image/png");
        image.Source.Data.Should().Be("AAAA");
    }

    [Fact]
    public void Handles_multimodal_remote_url_image()
    {
        var messages = new List<ChatMessage>
        {
            Msg("user", new object[]
            {
                new { type = "image_url", image_url = new { url = "https://example.com/cat.png" } },
            }),
        };

        var result = PartsBuilder.Build(messages);

        var image = result.Parts.Single(p => p.Type == "image");
        image.Source!.Type.Should().Be("url");
        image.Source.Url.Should().Be("https://example.com/cat.png");
    }

    [Fact]
    public void Drops_tool_role_messages_and_flags_result()
    {
        var messages = new List<ChatMessage>
        {
            Msg("user", "run tool"),
            new() { Role = "tool", ToolCallId = "call_123", Content = JsonSerializer.SerializeToElement("stdout output") },
        };

        var result = PartsBuilder.Build(messages);

        result.DroppedToolFields.Should().BeTrue();
        result.Parts.Should().NotContain(p => p.Type == "tool-result" || p.Type == "tool-call" || p.Type == "tools");
    }

    [Fact]
    public void Never_emits_tool_parts()
    {
        var messages = new List<ChatMessage>
        {
            Msg("user", "hello"),
            Msg("assistant", "hi there"),
        };

        var result = PartsBuilder.Build(messages);

        result.Parts.Should().OnlyContain(p => p.Type == "text" || p.Type == "system" || p.Type == "image");
    }
}
