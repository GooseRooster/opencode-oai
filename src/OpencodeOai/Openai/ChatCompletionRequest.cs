using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OpencodeOai.OpenCode.Models;

namespace OpencodeOai.Openai;

/// <summary>
/// Minimal OpenAI chat request shape. Content is either a string or an array of parts,
/// which we hold as a raw JsonElement to avoid AOT-hostile polymorphism.
/// </summary>
public sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("messages")]
    public List<ChatMessage>? Messages { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    // Ignored; kept only so client-side JSON stringifying doesn't fail us
    [JsonPropertyName("tools")]
    public JsonElement? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public JsonElement? ToolChoice { get; set; }

    // Accepted for OpenAI-client compatibility but not forwarded — OpenCode has
    // no per-request reasoning-effort knob. Reasoning depth is selected via the
    // model ID (e.g. `github-copilot/gpt-5-thinking`).
    [JsonPropertyName("reasoning_effort")]
    public JsonElement? ReasoningEffort { get; set; }
}

public sealed class ChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    /// <summary>Either a string or an array of content parts (text/image_url).</summary>
    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}
