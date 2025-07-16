using Newtonsoft.Json;

namespace OllamaYarpProject;

public class ChatCompletionRequest
{
    [JsonProperty("messages")]
    public List<ChatCompletionMessage> Messages { get; set; } = new();

    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("temperature")]
    public double? Temperature { get; set; }

    [JsonProperty("top_p")]
    public double? TopP { get; set; }

    [JsonProperty("n")]
    public int? N { get; set; }

    [JsonProperty("stream")]
    public bool? Stream { get; set; }

    [JsonProperty("stream_options")]
    public ChatStreamOptions? StreamOptions { get; set; }
}

public class ChatCompletionMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;
}

public class ChatStreamOptions
{
    [JsonProperty("include_usage")]
    public bool? IncludeUsage { get; set; }
}