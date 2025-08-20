using System.Text.Json;

namespace OllamaYarpProject.Interfaces;

/// <summary>
/// Generic interface for processing streaming responses with parsed SSE chunks
/// </summary>
public interface IStreamingResponseProcessor
{
    string Name { get; }
    
    /// <summary>
    /// Determines if this processor should handle the response
    /// </summary>
    bool ShouldProcess(HttpContext context, string modelName);
    
    /// <summary>
    /// Process a parsed SSE chunk and potentially modify it
    /// </summary>
    /// <param name="chunk">The parsed ChatCompletionChunk object</param>
    /// <returns>Modified chunk or null to keep original</returns>
    ChatCompletionChunk? ProcessChunk(ChatCompletionChunk chunk);
    
    /// <summary>
    /// Called when streaming is complete to generate final content
    /// </summary>
    string? GetFinalContent();
}

/// <summary>
/// SSE chunk data structure
/// </summary>
public class ChatCompletionChunk
{
    public string Id { get; set; } = "";
    public long Created { get; set; }
    public string Model { get; set; } = "";
    public string Object { get; set; } = "";
    public List<Choice> Choices { get; set; } = new();
    public List<string> Citations { get; set; } = new();
}

public class Choice
{
    public int Index { get; set; }
    public Delta Delta { get; set; } = new();
}

public class Delta
{
    public string Content { get; set; } = "";
}

/// <summary>
/// Factory for streaming response processors
/// </summary>
public interface IStreamingResponseProcessorFactory
{
    IStreamingResponseProcessor? GetProcessor(string modelName);
}

/// <summary>
/// Parsed SSE event data
/// </summary>
public class SseEvent
{
    public string EventType { get; set; } = "data";
    public string Data { get; set; } = "";
    public bool IsJsonData => EventType == "data" && !string.IsNullOrEmpty(Data) && Data.Trim() != "[DONE]";
    public bool IsDone => EventType == "data" && Data.Trim() == "[DONE]";
}