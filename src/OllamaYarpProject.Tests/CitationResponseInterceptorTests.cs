using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using OllamaYarpProject.Interceptors;
using OllamaYarpProject.Interfaces;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace OllamaYarpProject.Tests;

public class CitationResponseInterceptorTests
{
    private readonly Mock<ILogger<CitationResponseInterceptor>> _loggerMock;
    private readonly Mock<IChunkManipulatorFactory> _chunkManipulatorFactoryMock;
    private readonly Mock<IChunkManipulator> _chunkManipulatorMock;
    private readonly CitationResponseInterceptor _interceptor;

    public CitationResponseInterceptorTests()
    {
        _loggerMock = new Mock<ILogger<CitationResponseInterceptor>>();
        _chunkManipulatorFactoryMock = new Mock<IChunkManipulatorFactory>();
        _chunkManipulatorMock = new Mock<IChunkManipulator>();
        
        _chunkManipulatorFactoryMock.Setup(f => f.CreateChunkManipulator())
            .Returns(_chunkManipulatorMock.Object);
            
        _interceptor = new CitationResponseInterceptor(_loggerMock.Object, _chunkManipulatorFactoryMock.Object);
    }

    [Fact]
    public async Task ShouldInterceptAsync_WithStreamingResponse_ReturnsTrue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var response = new HttpResponseMessage();
        response.Content = new StringContent("", Encoding.UTF8, "text/event-stream");

        // Act
        var result = await _interceptor.ShouldInterceptAsync(httpContext, response);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldInterceptAsync_WithNonStreamingResponse_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var response = new HttpResponseMessage();
        response.Content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var result = await _interceptor.ShouldInterceptAsync(httpContext, response);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task InterceptAsync_WithSSEChunks_PreservesAllChunks()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        // Simulate SSE response with multiple chunks
        var sseContent = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"}}]}\n\n" +
                        "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\" world\"}}]}\n\n" +
                        "data: [DONE]\n\n";
        
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream");

        // Setup ChunkManipulator to return null for chunks without citations (simulating the real behavior)
        _chunkManipulatorMock.Setup(cm => cm.ProcessChunk(It.IsAny<string>()))
            .Returns((string chunk) => null); // This simulates the issue - returning null
        _chunkManipulatorMock.Setup(cm => cm.GetFinalChunk())
            .Returns((string?)null);

        // Act
        await _interceptor.InterceptAsync(httpContext, response);

        // Assert
        responseBody.Position = 0;
        var responseText = await new StreamReader(responseBody).ReadToEndAsync();
        
        // The response should NOT be empty - this test should fail before the fix
        Assert.NotEmpty(responseText);
        Assert.Contains("Hello", responseText);
        Assert.Contains("world", responseText);
    }

    [Fact]
    public async Task InterceptAsync_WithChunkManipulatorReturningNull_ShouldStillForwardOriginalChunk()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var sseContent = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Test content\"}}]}\n\n";
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream");

        // ChunkManipulator returns null (no processing needed)
        _chunkManipulatorMock.Setup(cm => cm.ProcessChunk(It.IsAny<string>()))
            .Returns((string?)null);
        _chunkManipulatorMock.Setup(cm => cm.GetFinalChunk())
            .Returns((string?)null);

        // Act
        await _interceptor.InterceptAsync(httpContext, response);

        // Assert
        responseBody.Position = 0;
        var responseText = await new StreamReader(responseBody).ReadToEndAsync();
        
        // This test reveals the bug: when ChunkManipulator returns null, 
        // the original chunk should still be forwarded
        Assert.Equal(sseContent, responseText);
    }

    [Fact]
    public async Task InterceptAsync_WithModifiedChunk_ForwardsModifiedContent()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var originalChunk = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Original[1]\"}}]}\n\n";
        var modifiedChunk = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Original\"}}]}\n\n";
        
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = new StringContent(originalChunk, Encoding.UTF8, "text/event-stream");

        // ChunkManipulator processes and modifies any chunk containing the original content
        _chunkManipulatorMock.Setup(cm => cm.ProcessChunk(It.Is<string>(s => s.Contains("Original[1]"))))
            .Returns(modifiedChunk);
        _chunkManipulatorMock.Setup(cm => cm.ProcessChunk(It.Is<string>(s => !s.Contains("Original[1]"))))
            .Returns((string?)null);
        _chunkManipulatorMock.Setup(cm => cm.GetFinalChunk())
            .Returns("Cit: [1](http://example.com)");

        // Act
        await _interceptor.InterceptAsync(httpContext, response);

        // Assert
        responseBody.Position = 0;
        var responseText = await new StreamReader(responseBody).ReadToEndAsync();
        
        Assert.Contains("Original", responseText);
        // The modified chunk should not contain the citation marker in the content
        Assert.DoesNotContain("Original[1]", responseText); 
        Assert.Contains("Cit: [1](http://example.com)", responseText);
    }
}