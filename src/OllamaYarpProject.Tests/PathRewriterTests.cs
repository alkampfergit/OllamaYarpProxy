using Microsoft.Extensions.Logging;
using Moq;
using OllamaYarpProject.Interfaces;
using Xunit;

namespace OllamaYarpProject.Tests;

public class PathRewriterTests
{
    private readonly Mock<ILogger<StandardPathRewriter>> _loggerMock;
    private readonly StandardPathRewriter _pathRewriter;

    public PathRewriterTests()
    {
        _loggerMock = new Mock<ILogger<StandardPathRewriter>>();
        _pathRewriter = new StandardPathRewriter(_loggerMock.Object);
    }

    [Theory]
    [InlineData("/api/tags", "GET", "/models")]
    [InlineData("/api/tags", "POST", "/models")]
    [InlineData("/v1/chat/completions", "POST", "/chat/completions")]
    [InlineData("/v1/chat/completions", "GET", "/chat/completions")]
    public void RewritePath_WithKnownPaths_ReturnsCorrectRewrite(string originalPath, string method, string expected)
    {
        // Act
        var result = _pathRewriter.RewritePath(originalPath, method);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/api/version", "GET")]
    [InlineData("/api/show", "POST")]
    [InlineData("/some/other/path", "GET")]
    [InlineData("/", "GET")]
    [InlineData("", "POST")]
    public void RewritePath_WithUnknownPaths_ReturnsNull(string originalPath, string method)
    {
        // Act
        var result = _pathRewriter.RewritePath(originalPath, method);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("/api/tags")]
    [InlineData("/v1/chat/completions")]
    public void ShouldRewrite_WithKnownPaths_ReturnsTrue(string path)
    {
        // Act
        var result = _pathRewriter.ShouldRewrite(path);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("/api/version")]
    [InlineData("/api/show")]
    [InlineData("/some/other/path")]
    [InlineData("/")]
    [InlineData("")]
    public void ShouldRewrite_WithUnknownPaths_ReturnsFalse(string path)
    {
        // Act
        var result = _pathRewriter.ShouldRewrite(path);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RewritePath_LogsRewriteOperations()
    {
        // Arrange
        var originalPath = "/api/tags";
        var method = "GET";

        // Act
        _pathRewriter.RewritePath(originalPath, method);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("PATH REWRITE")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("/API/TAGS", "GET")] // Case sensitivity test
    [InlineData("/api/tags/", "GET")] // Trailing slash test
    public void RewritePath_CaseSensitive_ReturnsNullForNonExactMatch(string originalPath, string method)
    {
        // Act
        var result = _pathRewriter.RewritePath(originalPath, method);

        // Assert
        Assert.Null(result);
    }
}