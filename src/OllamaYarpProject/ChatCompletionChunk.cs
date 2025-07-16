using Newtonsoft.Json;

namespace OllamaYarpProject;

public class ChatCompletionChunk
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("created")]
    public long Created { get; set; }

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [JsonProperty("choices")]
    public List<ChatCompletionChunkChoice> Choices { get; set; } = new();

    [JsonProperty("stream_options")]
    public ChatStreamOptions? StreamOptions { get; set; }
}

public class ChatCompletionChunkChoice
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("delta")]
    public ChatCompletionDelta Delta { get; set; } = new();

    [JsonProperty("finish_reason")]
    public string? FinishReason { get; set; }
}

public class ChatCompletionDelta
{
    [JsonProperty("content")]
    public string? Content { get; set; }

    [JsonProperty("role")]
    public string? Role { get; set; }

    [JsonProperty("function_call")]
    public object? FunctionCall { get; set; }

    [JsonProperty("tool_calls")]
    public object? ToolCalls { get; set; }
}