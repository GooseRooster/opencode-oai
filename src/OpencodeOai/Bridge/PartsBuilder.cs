using System.Text;
using System.Text.Json;
using OpencodeOai.OpenCode.Models;
using OpencodeOai.Openai;

namespace OpencodeOai.Bridge;

/// <summary>
/// Converts OpenAI-shaped messages into an OpenCode <see cref="PartDto"/> list
/// plus a system prompt string. OpenCode expects the system prompt on the
/// request's top-level <c>system</c> field, not as a part.
///
/// Tool-related fields are silently dropped (this bridge does not support tool
/// calling — see README).
/// </summary>
internal static class PartsBuilder
{
    public static BuildResult Build(IReadOnlyList<ChatMessage> messages)
    {
        var parts = new List<PartDto>();
        var systemBuf = new StringBuilder();
        var hasImage = false;
        var droppedTool = false;

        // Collect system + developer messages into a single system prompt.
        foreach (var m in messages)
        {
            if (m.Role != "system" && m.Role != "developer") continue;
            var text = ExtractText(m.Content);
            if (string.IsNullOrEmpty(text)) continue;
            if (systemBuf.Length > 0) systemBuf.Append('\n');
            systemBuf.Append(text);
        }

        foreach (var m in messages)
        {
            if (m.Role == "system" || m.Role == "developer") continue;

            if (m.Role == "tool")
            {
                droppedTool = true;
                continue;
            }

            var roleLabel = m.Role.ToUpperInvariant();

            if (m.Content is null)
            {
                continue;
            }

            var content = m.Content.Value;
            if (content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString() ?? "";
                parts.Add(new PartDto { Type = "text", Text = $"[{roleLabel}]\n{text}" });
                continue;
            }

            if (content.ValueKind == JsonValueKind.Array)
            {
                parts.Add(new PartDto { Type = "text", Text = $"[{roleLabel}]" });

                foreach (var item in content.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    if (!item.TryGetProperty("type", out var typeElem)) continue;
                    var type = typeElem.GetString();

                    if (type == "text" && item.TryGetProperty("text", out var textElem))
                    {
                        parts.Add(new PartDto { Type = "text", Text = textElem.GetString() ?? "" });
                    }
                    else if (type == "image_url" && item.TryGetProperty("image_url", out var imgElem)
                          && imgElem.TryGetProperty("url", out var urlElem))
                    {
                        var url = urlElem.GetString() ?? "";
                        hasImage = true;

                        // OpenCode's FilePartInput is flat: { type: "file", mime, url }.
                        // Data URIs are passed through as-is; OpenCode parses them upstream.
                        var mime = GuessMime(url);
                        parts.Add(new PartDto
                        {
                            Type = "file",
                            Mime = mime,
                            Url = url,
                        });
                    }
                    // audio / file / tool-anything → silently skipped
                }
            }
        }

        var system = systemBuf.Length > 0 ? systemBuf.ToString() : null;
        return new BuildResult(parts, system, hasImage, droppedTool);
    }

    private static string GuessMime(string url)
    {
        if (url.StartsWith("data:", StringComparison.Ordinal))
        {
            var semi = url.IndexOf(';', StringComparison.Ordinal);
            var comma = url.IndexOf(',', StringComparison.Ordinal);
            var end = semi > 0 ? semi : comma;
            if (end > 5) return url[5..end];
        }
        // Best-effort suffix sniff for remote URLs.
        var lower = url.ToLowerInvariant();
        if (lower.EndsWith(".png",  StringComparison.Ordinal)) return "image/png";
        if (lower.EndsWith(".jpg",  StringComparison.Ordinal) || lower.EndsWith(".jpeg", StringComparison.Ordinal)) return "image/jpeg";
        if (lower.EndsWith(".gif",  StringComparison.Ordinal)) return "image/gif";
        if (lower.EndsWith(".webp", StringComparison.Ordinal)) return "image/webp";
        return "image/*";
    }

    private static string ExtractText(JsonElement? content)
    {
        if (content is null) return "";
        var c = content.Value;
        if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? "";
        if (c.ValueKind == JsonValueKind.Array)
        {
            var buf = new StringBuilder();
            foreach (var item in c.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var t))
                {
                    if (buf.Length > 0) buf.Append('\n');
                    buf.Append(t.GetString());
                }
            }
            return buf.ToString();
        }
        return "";
    }
}

internal readonly record struct BuildResult(
    List<PartDto> Parts,
    string? System,
    bool HasImage,
    bool DroppedToolFields);
