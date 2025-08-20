using System.Text;
using System.Text.Json;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject.Services;

/// <summary>
/// Generic Server-Sent Events (SSE) parser that handles streaming responses
/// </summary>
public interface ISseParser
{
    IAsyncEnumerable<SseEvent> ParseSseStreamAsync(Stream stream, CancellationToken cancellationToken = default);
    ChatCompletionChunk? ParseChatCompletionChunk(string jsonData);
    string SerializeChatCompletionChunk(ChatCompletionChunk chunk);
}

public class SseParser : ISseParser
{
    private readonly ILogger<SseParser> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SseParser(ILogger<SseParser> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<SseEvent> ParseSseStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var currentEvent = new SseEvent();
        var dataBuilder = new StringBuilder();

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            // Empty line indicates end of event
            if (string.IsNullOrEmpty(line))
            {
                if (dataBuilder.Length > 0)
                {
                    currentEvent.Data = dataBuilder.ToString();
                    yield return currentEvent;
                    
                    // Reset for next event
                    currentEvent = new SseEvent();
                    dataBuilder.Clear();
                }
                continue;
            }

            // Parse SSE line
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6);
                if (dataBuilder.Length > 0)
                    dataBuilder.AppendLine();
                dataBuilder.Append(data);
            }
            else if (line.StartsWith("event: "))
            {
                currentEvent.EventType = line.Substring(7);
            }
            else if (line.StartsWith("id: "))
            {
                // We can add ID handling later if needed
            }
            else if (line.StartsWith("retry: "))
            {
                // We can add retry handling later if needed
            }
        }

        // Handle any remaining event data
        if (dataBuilder.Length > 0)
        {
            currentEvent.Data = dataBuilder.ToString();
            yield return currentEvent;
        }
    }

    public ChatCompletionChunk? ParseChatCompletionChunk(string jsonData)
    {
        try
        {
            return JsonSerializer.Deserialize<ChatCompletionChunk>(jsonData, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug("[SSE PARSER] Failed to parse ChatCompletionChunk: {Error}", ex.Message);
            return null;
        }
    }

    public string SerializeChatCompletionChunk(ChatCompletionChunk chunk)
    {
        try
        {
            return JsonSerializer.Serialize(chunk, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError("[SSE PARSER] Failed to serialize ChatCompletionChunk: {Error}", ex.Message);
            throw;
        }
    }
}