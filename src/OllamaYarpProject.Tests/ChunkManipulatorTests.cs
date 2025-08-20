using Microsoft.Extensions.Logging;
using OllamaYarpProject;
using OllamaYarpProject.Interfaces;
using System.Text.Json;
using Xunit;
using Moq;

namespace OllamaYarpProject.Tests;

public class ChunkManipulatorTests
{
    private readonly Mock<ILogger<ChunkManipulator>> _loggerMock;
    private readonly ChunkManipulator _chunkManipulator;

    public ChunkManipulatorTests()
    {
        _loggerMock = new Mock<ILogger<ChunkManipulator>>();
        _chunkManipulator = new ChunkManipulator(_loggerMock.Object);
    }

    [Fact]
    public void ProcessChunk_WithNonJsonChunk_ReturnsNullForNoCR()
    {
        // Arrange
        var chunk = "Some non-JSON text without carriage return";

        // Act
        var result = _chunkManipulator.ProcessChunk(chunk);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ProcessChunk_WithNonJsonChunkAndCR_ReturnsChunk()
    {
        // Arrange
        var chunk = "Some non-JSON text with carriage return\n";

        // Act
        var result = _chunkManipulator.ProcessChunk(chunk);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Some non-JSON text with carriage return", result);
    }

    [Fact]
    public void ProcessChunk_WithValidSSEChunk_ProcessesCorrectly()
    {
        // Arrange
        var chunkData = new ChatCompletionChunk
        {
            Id = "test-id",
            Created = 1234567890,
            Model = "test-model",
            Object = "chat.completion.chunk",
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Delta = new Delta { Content = "Hello world\n" }
                }
            },
            Citations = new List<string> { "https://example.com" }
        };

        var json = JsonSerializer.Serialize(chunkData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var chunk = $"data: {json}\n\n";

        // Act
        var result = _chunkManipulator.ProcessChunk(chunk);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Hello world", result);
    }

    [Fact]
    public void ProcessChunk_WithCitationsInContent_ReplacesCitationsWithLinks()
    {
        // Arrange
        var chunkData = new ChatCompletionChunk
        {
            Id = "test-id",
            Created = 1234567890,
            Model = "test-model",
            Object = "chat.completion.chunk",
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Delta = new Delta { Content = "Check this reference [1]\n" }
                }
            },
            Citations = new List<string> { "https://example.com" }
        };

        var json = JsonSerializer.Serialize(chunkData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var chunk = $"data: {json}\n\n";

        // Act
        var result = _chunkManipulator.ProcessChunk(chunk);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Check this reference", result);
        Assert.Contains("Cit: [1](https://example.com)", result);
        Assert.DoesNotContain("[1]", result.Replace("Cit: [1](https://example.com)", ""));
    }

    [Fact]
    public void GetFinalChunk_WithAccumulatedContent_ReturnsFinalChunk()
    {
        // Arrange - Process some chunks first
        _chunkManipulator.ProcessChunk("Some accumulated content without CR");

        // Act
        var result = _chunkManipulator.GetFinalChunk();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Some accumulated content without CR", result);
    }

    [Fact]
    public void GetFinalChunk_WithNoCitationsAccumulated_ReturnsContentWithoutCitations()
    {
        // Arrange
        _chunkManipulator.ProcessChunk("Simple content");

        // Act
        var result = _chunkManipulator.GetFinalChunk();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Simple content", result);
        Assert.DoesNotContain("Cit:", result);
    }

    [Fact]
    public void ProcessChunk_WithInvalidJson_HandlesGracefully()
    {
        // Arrange
        var chunk = "data: {invalid json content\n\n";

        // Act
        var result = _chunkManipulator.ProcessChunk(chunk);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("data: {invalid json content", result);
    }

    [Fact]
    public void ProcessChunk_MultipleCitations_ProcessesAllCorrectly()
    {
        // Arrange
        var chunkData = new ChatCompletionChunk
        {
            Id = "test-id",
            Created = 1234567890,
            Model = "test-model",
            Object = "chat.completion.chunk",
            Choices = new List<Choice>
            {
                new Choice
                {
                    Index = 0,
                    Delta = new Delta { Content = "See [1] and [2]\n" }
                }
            },
            Citations = new List<string> { "https://example.com", "https://example2.com" }
        };

        var json = JsonSerializer.Serialize(chunkData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var chunk = $"data: {json}\n\n";

        // Act
        var result = _chunkManipulator.ProcessChunk(chunk);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("See", result);
        Assert.Contains("and", result);
        Assert.Contains("Cit: [1](https://example.com), [2](https://example2.com)", result);
    }
}