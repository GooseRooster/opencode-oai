using System.Text.Json;
using OpencodeOai.OpenCode.Models;
using OpencodeOai.Openai;

namespace OpencodeOai.Bridge;

/// <summary>
/// Converts OpenAI-shaped messages into an OpenCode <see cref="PartDto"/> list.
/// Text and image parts only. Tool-related fields are silently dropped
/// (this bridge does not support tool calling — see README).
/// </summary>
internal static class PartsBuilder
{
    public static BuildResult Build(IReadOnlyList<ChatMessage> messages)
    {
        var parts = new List<PartDto>();
        var hasImage = false;
        var droppedTool = false;

        // System message first, as a dedicated "system" part.
        for (var i = 0; i < messages.Count; i++)
        {
            var m = messages[i];
            if (m.Role == "system")
            {
                var text = ExtractText(m.Content);
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(new PartDto { Type = "system", Text = text });
                }
                break;
            }
        }

        foreach (var m in messages)
        {
            if (m.Role == "system") continue;

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

                        if (url.StartsWith("data:", StringComparison.Ordinal))
                        {
                            var comma = url.IndexOf(',', StringComparison.Ordinal);
                            if (comma <= 0) continue;
                            var meta = url[..comma];
                            var data = url[(comma + 1)..];
                            var mediaType = meta.Replace("data:", "", StringComparison.Ordinal)
                                                .Replace(";base64", "", StringComparison.Ordinal);
                            parts.Add(new PartDto
                            {
                                Type = "image",
                                Source = new ImageSourceDto { Type = "base64", MediaType = mediaType, Data = data },
                            });
                        }
                        else
                        {
                            parts.Add(new PartDto
                            {
                                Type = "image",
                                Source = new ImageSourceDto { Type = "url", Url = url },
                            });
                        }
                    }
                    // audio / file / tool-anything → silently skipped
                }
            }
        }

        return new BuildResult(parts, hasImage, droppedTool);
    }

    private static string ExtractText(JsonElement? content)
    {
        if (content is null) return "";
        var c = content.Value;
        if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? "";
        if (c.ValueKind == JsonValueKind.Array)
        {
            var buf = new System.Text.StringBuilder();
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

internal readonly record struct BuildResult(List<PartDto> Parts, bool HasImage, bool DroppedToolFields);
