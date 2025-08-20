using Microsoft.Extensions.Logging;
using Moq;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Processors;
using Xunit;

namespace OllamaYarpProject.Tests;

public class CitationStreamingProcessorTests
{
    private readonly Mock<ILogger<CitationStreamingProcessor>> _loggerMock;
    private readonly CitationStreamingProcessor _processor;

    public CitationStreamingProcessorTests()
    {
        _loggerMock = new Mock<ILogger<CitationStreamingProcessor>>();
        _processor = new CitationStreamingProcessor(_loggerMock.Object);
    }

    [Fact]
    public void ShouldProcess_ReturnsTrue()
    {
        // Act
        var result = _processor.ShouldProcess(null!, "any-model");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ProcessChunk_WithCitationMarkers_RemovesCitationMarkers()
    {
        // Arrange
        var chunk = new ChatCompletionChunk
        {
            Choices = new List<Choice>
            {
                new() { Index = 0, Delta = new Delta { Content = "Hello[1] world[2]!" } }
            },
            Citations = new List<string> { "http://example.com", "http://test.com" }
        };

        // Act
        var result = _processor.ProcessChunk(chunk);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello world!", result.Choices[0].Delta.Content);
    }

    [Fact]
    public void ProcessChunk_WithoutCitationMarkers_ReturnsNull()
    {
        // Arrange
        var chunk = new ChatCompletionChunk
        {
            Choices = new List<Choice>
            {
                new() { Index = 0, Delta = new Delta { Content = "Hello world!" } }
            }
        };

        // Act
        var result = _processor.ProcessChunk(chunk);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetFinalContent_WithCollectedCitations_ReturnsFormattedCitations()
    {
        // Arrange
        var chunk = new ChatCompletionChunk
        {
            Choices = new List<Choice>
            {
                new() { Index = 0, Delta = new Delta { Content = "Test[1]" } }
            },
            Citations = new List<string> { "http://example.com" }
        };

        // Process a chunk to collect citations
        _processor.ProcessChunk(chunk);

        // Act
        var finalContent = _processor.GetFinalContent();

        // Assert
        Assert.NotNull(finalContent);
        Assert.Contains("Cit: [1](http://example.com)", finalContent);
    }

    [Fact]
    public void GetFinalContent_WithoutCitations_ReturnsNull()
    {
        // Act
        var finalContent = _processor.GetFinalContent();

        // Assert
        Assert.Null(finalContent);
    }
}