using System.Text.Json;
using FluentAssertions;
using OpencodeOai.Logging;
using OpencodeOai.Openai;
using Xunit;

namespace OpencodeOai.Tests;

public class LogPreviewTests
{
    private static ChatMessage Msg(string role, object content) => new()
    {
        Role = role,
        Content = JsonSerializer.SerializeToElement(content),
    };

    [Fact]
    public void Renders_messages_as_role_prefixed_segments()
    {
        var preview = LogPreview.Messages(
            [Msg("system", "Be terse."), Msg("user", "hi")],
            maxChars: 0);

        preview.Should().Be("system: Be terse. | user: hi");
    }

    [Fact]
    public void Collapses_newlines_onto_a_single_line()
    {
        var preview = LogPreview.Truncate("line one\n\n  line two\t", maxChars: 0);

        preview.Should().Be("line one line two");
    }

    [Fact]
    public void Truncates_and_reports_withheld_length()
    {
        var preview = LogPreview.Truncate(new string('x', 20), maxChars: 5);

        preview.Should().Be("xxxxx… (+15 chars)");
    }

    [Fact]
    public void Does_not_truncate_when_cap_is_zero_or_negative()
    {
        var text = new string('x', 5_000);

        LogPreview.Truncate(text, maxChars: 0).Should().HaveLength(5_000);
        LogPreview.Truncate(text, maxChars: -1).Should().HaveLength(5_000);
    }

    [Fact]
    public void Elides_image_parts_instead_of_dumping_data_uris()
    {
        var preview = LogPreview.Messages(
            [
                Msg("user", new object[]
                {
                    new { type = "text", text = "what is this" },
                    new { type = "image_url", image_url = new { url = "data:image/png;base64,AAAA" } },
                }),
            ],
            maxChars: 0);

        preview.Should().Be("user: what is this <image>");
        preview.Should().NotContain("base64");
    }

    [Fact]
    public void Handles_null_content()
    {
        var preview = LogPreview.Messages([new ChatMessage { Role = "assistant" }], maxChars: 0);

        preview.Should().Be("assistant:");
    }
}
