using Microsoft.Extensions.Logging;
using Moq;
using Ollama;
using OllamaYarpProject.Interfaces;
using System.Threading;
using Xunit;

namespace OllamaYarpProject.Tests;

public class ModelRouterTests
{
    private readonly Mock<ILogger<ModelRouter>> _loggerMock;
    private readonly Mock<IDateTime> _dateTimeMock;
    private readonly List<IModel> _mockModels;
    private readonly ModelRouter _modelRouter;

    public ModelRouterTests()
    {
        _loggerMock = new Mock<ILogger<ModelRouter>>();
        _dateTimeMock = new Mock<IDateTime>();
        _mockModels = new List<IModel>();
        _modelRouter = new ModelRouter(_mockModels, _loggerMock.Object, _dateTimeMock.Object);

        _dateTimeMock.Setup(x => x.UtcNow).Returns(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetCustomModelAsync_WithExistingModel_ReturnsModel()
    {
        // Arrange
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("test-model");
        _mockModels.Add(mockModel.Object);

        // Act
        var result = await _modelRouter.GetCustomModelAsync("test-model");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-model", result.Name);
    }

    [Fact]
    public async Task GetCustomModelAsync_WithNonExistingModel_ReturnsNull()
    {
        // Arrange
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("test-model");
        _mockModels.Add(mockModel.Object);

        // Act
        var result = await _modelRouter.GetCustomModelAsync("non-existing-model");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCustomModelAsync_WithNullOrEmptyModelName_ReturnsNull(string? modelName)
    {
        // Act
        var result = await _modelRouter.GetCustomModelAsync(modelName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCustomModelAsync_CaseInsensitive_ReturnsModel()
    {
        // Arrange
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("Test-Model");
        _mockModels.Add(mockModel.Object);

        // Act
        var result = await _modelRouter.GetCustomModelAsync("test-model");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test-Model", result.Name);
    }

    [Fact]
    public async Task GenerateDirectResponseAsync_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("test-model");
        mockModel.Setup(m => m.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Generated response");

        var request = new GenerateChatCompletionRequest
        {
            Model = "test-model",
            Messages = new List<Message>
            {
                new Message { Role = MessageRole.User, Content = "Hello" },
                new Message { Role = MessageRole.Assistant, Content = "Hi there!" }
            }
        };

        // Act
        var result = await _modelRouter.GenerateDirectResponseAsync(mockModel.Object, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-model", result.Model);
        Assert.Equal("Generated response", result.Message.Content);
        Assert.Equal(MessageRole.Assistant, result.Message.Role);
        Assert.True(result.Done);
        Assert.Equal(DoneReasonEnum.Stop, result.DoneReason);
        Assert.Equal(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc), result.CreatedAt);
    }

    [Fact]
    public async Task GenerateDirectResponseAsync_WithEmptyMessages_CallsModelWithEmptyPrompt()
    {
        // Arrange
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("test-model");
        mockModel.Setup(m => m.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Response");

        var request = new GenerateChatCompletionRequest
        {
            Model = "test-model",
            Messages = null
        };

        // Act
        var result = await _modelRouter.GenerateDirectResponseAsync(mockModel.Object, request);

        // Assert
        Assert.NotNull(result);
        mockModel.Verify(m => m.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateDirectResponseAsync_FormatsMessagesCorrectly()
    {
        // Arrange
        var capturedPrompt = string.Empty;
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("test-model");
        mockModel.Setup(m => m.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((prompt, _) => capturedPrompt = prompt)
                .ReturnsAsync("Response");

        var request = new GenerateChatCompletionRequest
        {
            Model = "test-model",
            Messages = new List<Message>
            {
                new Message { Role = MessageRole.User, Content = "Hello" },
                new Message { Role = MessageRole.Assistant, Content = "Hi there!" }
            }
        };

        // Act
        await _modelRouter.GenerateDirectResponseAsync(mockModel.Object, request);

        // Assert
        Assert.Contains("Role: User", capturedPrompt);
        Assert.Contains("Hello", capturedPrompt);
        Assert.Contains("Role: Assistant", capturedPrompt);
        Assert.Contains("Hi there!", capturedPrompt);
        Assert.Contains("-------", capturedPrompt);
    }

    [Fact]
    public async Task GenerateDirectResponseAsync_ModelThrowsException_ReturnsNull()
    {
        // Arrange
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("test-model");
        mockModel.Setup(m => m.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Model error"));

        var request = new GenerateChatCompletionRequest
        {
            Model = "test-model",
            Messages = new List<Message>()
        };

        // Act
        var result = await _modelRouter.GenerateDirectResponseAsync(mockModel.Object, request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateDirectResponseAsync_ModelThrowsException_LogsError()
    {
        // Arrange
        var mockModel = new Mock<IModel>();
        mockModel.Setup(m => m.Name).Returns("test-model");
        mockModel.Setup(m => m.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Model error"));

        var request = new GenerateChatCompletionRequest
        {
            Model = "test-model",
            Messages = new List<Message>()
        };

        // Act
        await _modelRouter.GenerateDirectResponseAsync(mockModel.Object, request);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error generating response")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}