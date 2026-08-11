using System.Text;
using System.Text.Json;
using OpencodeOai.Openai;

namespace OpencodeOai.Logging;

/// <summary>
/// Renders prompt and completion text into single-line, length-capped previews.
///
/// Content previews are opt-in (<c>OPENCODE_OAI_LOG_PROMPTS</c>) because prompts
/// routinely carry user source code; request/response *metadata* is always logged.
/// </summary>
internal static class LogPreview
{
    /// <summary>
    /// Flattens OpenAI messages into <c>role: text</c> segments. Image parts are
    /// rendered as <c>&lt;image&gt;</c> rather than dumping base64 data URIs.
    /// </summary>
    public static string Messages(IReadOnlyList<ChatMessage> messages, int maxChars)
    {
        var buf = new StringBuilder();

        foreach (var m in messages)
        {
            if (buf.Length > 0) buf.Append(" | ");
            buf.Append(m.Role).Append(": ").Append(Flatten(m.Content));

            // Stop building once we're past the cap — Truncate does the trimming.
            if (maxChars > 0 && buf.Length > maxChars) break;
        }

        return Truncate(buf.ToString(), maxChars);
    }

    /// <summary>
    /// Collapses whitespace to keep the preview on one line and caps it at
    /// <paramref name="maxChars"/> (0 or less means unbounded), appending the
    /// number of characters withheld.
    /// </summary>
    public static string Truncate(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var collapsed = Collapse(text);
        if (maxChars <= 0 || collapsed.Length <= maxChars) return collapsed;

        return $"{collapsed[..maxChars]}… (+{collapsed.Length - maxChars} chars)";
    }

    private static string Flatten(JsonElement? content)
    {
        if (content is null) return "";
        var c = content.Value;

        if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? "";
        if (c.ValueKind != JsonValueKind.Array) return "";

        var buf = new StringBuilder();
        foreach (var item in c.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            var type = item.TryGetProperty("type", out var typeElem) ? typeElem.GetString() : null;
            var segment = type switch
            {
                "text" when item.TryGetProperty("text", out var t) => t.GetString() ?? "",
                "image_url" => "<image>",
                null => "",
                _ => $"<{type}>",
            };

            if (segment.Length == 0) continue;
            if (buf.Length > 0) buf.Append(' ');
            buf.Append(segment);
        }

        return buf.ToString();
    }

    private static string Collapse(string text)
    {
        var buf = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = buf.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                buf.Append(' ');
                pendingSpace = false;
            }

            buf.Append(ch);
        }

        return buf.ToString();
    }
}
