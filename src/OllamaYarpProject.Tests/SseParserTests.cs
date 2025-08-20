using Microsoft.Extensions.Logging;
using Moq;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Services;
using System.Text;
using Xunit;

namespace OllamaYarpProject.Tests;

public class SseParserTests
{
    private readonly Mock<ILogger<SseParser>> _loggerMock;
    private readonly SseParser _sseParser;

    public SseParserTests()
    {
        _loggerMock = new Mock<ILogger<SseParser>>();
        _sseParser = new SseParser(_loggerMock.Object);
    }

    [Fact]
    public async Task ParseSseStreamAsync_WithSimpleSseData_ParsesCorrectly()
    {
        // Arrange
        var sseContent = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"}}]}\n\n" +
                        "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\" world\"}}]}\n\n" +
                        "data: [DONE]\n\n";
        
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseContent));

        // Act
        var events = new List<SseEvent>();
        await foreach (var sseEvent in _sseParser.ParseSseStreamAsync(stream))
        {
            events.Add(sseEvent);
        }

        // Assert
        Assert.Equal(3, events.Count);
        
        Assert.True(events[0].IsJsonData);
        Assert.Contains("Hello", events[0].Data);
        
        Assert.True(events[1].IsJsonData);
        Assert.Contains("world", events[1].Data);
        
        Assert.True(events[2].IsDone);
        Assert.Equal("[DONE]", events[2].Data);
    }

    [Fact]
    public void ParseChatCompletionChunk_WithValidJson_ParsesCorrectly()
    {
        // Arrange
        var jsonData = "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Test content\"}}]}";

        // Act
        var chunk = _sseParser.ParseChatCompletionChunk(jsonData);

        // Assert
        Assert.NotNull(chunk);
        Assert.Single(chunk.Choices);
        Assert.Equal("Test content", chunk.Choices[0].Delta.Content);
    }

    [Fact]
    public void ParseChatCompletionChunk_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var invalidJson = "invalid json content";

        // Act
        var chunk = _sseParser.ParseChatCompletionChunk(invalidJson);

        // Assert
        Assert.Null(chunk);
    }

    [Fact]
    public void SerializeChatCompletionChunk_WithValidChunk_SerializesCorrectly()
    {
        // Arrange
        var chunk = new ChatCompletionChunk
        {
            Choices = new List<Choice>
            {
                new() { Index = 0, Delta = new Delta { Content = "Test" } }
            }
        };

        // Act
        var json = _sseParser.SerializeChatCompletionChunk(chunk);

        // Assert
        Assert.Contains("Test", json);
        Assert.Contains("choices", json);
    }
}