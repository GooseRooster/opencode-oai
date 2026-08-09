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
        result.System.Should().BeNull();
    }

    [Fact]
    public void Extracts_system_message_onto_system_field_not_as_part()
    {
        var messages = new List<ChatMessage>
        {
            Msg("system", "You are a helpful assistant."),
            Msg("user", "hi"),
        };

        var result = PartsBuilder.Build(messages);

        result.System.Should().Be("You are a helpful assistant.");
        result.Parts.Should().NotContain(p => p.Type == "system");
        result.Parts.Should().OnlyContain(p => p.Type == "text" || p.Type == "file");
    }

    [Fact]
    public void Combines_multiple_system_and_developer_messages()
    {
        var messages = new List<ChatMessage>
        {
            Msg("system", "You are helpful."),
            Msg("developer", "Reply in JSON."),
            Msg("user", "hi"),
        };

        var result = PartsBuilder.Build(messages);

        result.System.Should().Be("You are helpful.\nReply in JSON.");
    }

    [Fact]
    public void Emits_file_part_for_data_uri_image()
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
        var image = result.Parts.Single(p => p.Type == "file");
        image.Mime.Should().Be("image/png");
        image.Url.Should().Be("data:image/png;base64,AAAA");
    }

    [Fact]
    public void Emits_file_part_for_remote_url_image()
    {
        var messages = new List<ChatMessage>
        {
            Msg("user", new object[]
            {
                new { type = "image_url", image_url = new { url = "https://example.com/cat.png" } },
            }),
        };

        var result = PartsBuilder.Build(messages);

        var image = result.Parts.Single(p => p.Type == "file");
        image.Mime.Should().Be("image/png");
        image.Url.Should().Be("https://example.com/cat.png");
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
    public void Only_emits_text_or_file_parts()
    {
        var messages = new List<ChatMessage>
        {
            Msg("system", "sys"),
            Msg("user", "hello"),
            Msg("assistant", "hi there"),
        };

        var result = PartsBuilder.Build(messages);

        result.Parts.Should().OnlyContain(p => p.Type == "text" || p.Type == "file");
    }
}
