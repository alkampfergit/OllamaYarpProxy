using Microsoft.Extensions.Logging;
using Moq;
using OllamaYarpProject.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OllamaYarpProject.Tests;

public class ResponseTransformerTests
{
    private readonly Mock<IJsonSerializer> _jsonSerializerMock;
    private readonly Mock<IDateTime> _dateTimeMock;
    private readonly Mock<ILogger<ResponseTransformer>> _loggerMock;
    private readonly ResponseTransformer _responseTransformer;
    private readonly List<IModel> _customModels;

    public ResponseTransformerTests()
    {
        _jsonSerializerMock = new Mock<IJsonSerializer>();
        _dateTimeMock = new Mock<IDateTime>();
        _loggerMock = new Mock<ILogger<ResponseTransformer>>();
        _customModels = new List<IModel>();
        _responseTransformer = new ResponseTransformer(_jsonSerializerMock.Object, _dateTimeMock.Object, _loggerMock.Object);

        _dateTimeMock.Setup(x => x.UtcNow).Returns(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task TransformModelsResponseAsync_WithValidSourceResponse_ReturnsTransformedResponse()
    {
        // Arrange
        var sourceResponse = """{"data":[{"id":"model1"},{"id":"model2"}]}""";
        var sourceRoot = new SourceRoot
        {
            data = new List<ModelData>
            {
                new ModelData { id = "model1" },
                new ModelData { id = "model2" }
            }
        };

        var mockCustomModel = new Mock<IModel>();
        mockCustomModel.Setup(m => m.Name).Returns("custom-model");
        _customModels.Add(mockCustomModel.Object);

        _jsonSerializerMock.Setup(j => j.Deserialize<SourceRoot>(sourceResponse))
                          .Returns(sourceRoot);
        _jsonSerializerMock.Setup(j => j.Serialize(It.IsAny<OllamaRoot>(), true))
                          .Returns("transformed json");

        // Act
        var result = await _responseTransformer.TransformModelsResponseAsync(sourceResponse, _customModels);

        // Assert
        Assert.Equal("transformed json", result);
        _jsonSerializerMock.Verify(j => j.Deserialize<SourceRoot>(sourceResponse), Times.Once);
        _jsonSerializerMock.Verify(j => j.Serialize(It.IsAny<OllamaRoot>(), true), Times.Once);
    }

    [Fact]
    public async Task TransformModelsResponseAsync_WithCustomModels_IncludesCustomModelsInResponse()
    {
        // Arrange
        var sourceResponse = """{"data":[{"id":"model1"}]}""";
        var sourceRoot = new SourceRoot
        {
            data = new List<ModelData> { new ModelData { id = "model1" } }
        };

        var mockCustomModel = new Mock<IModel>();
        mockCustomModel.Setup(m => m.Name).Returns("custom-model");
        _customModels.Add(mockCustomModel.Object);

        _jsonSerializerMock.Setup(j => j.Deserialize<SourceRoot>(sourceResponse))
                          .Returns(sourceRoot);

        OllamaRoot? capturedOllamaRoot = null;
        _jsonSerializerMock.Setup(j => j.Serialize(It.IsAny<OllamaRoot>(), true))
                          .Callback<object, bool>((obj, _) => capturedOllamaRoot = obj as OllamaRoot)
                          .Returns("transformed json");

        // Act
        await _responseTransformer.TransformModelsResponseAsync(sourceResponse, _customModels);

        // Assert
        Assert.NotNull(capturedOllamaRoot);
        Assert.Equal(2, capturedOllamaRoot.models.Count); // 1 from source + 1 custom
        Assert.Contains(capturedOllamaRoot.models, m => m.name == "model1");
        Assert.Contains(capturedOllamaRoot.models, m => m.name == "custom-model");
    }

    [Fact]
    public async Task TransformModelsResponseAsync_WithEmptySourceData_OnlyIncludesCustomModels()
    {
        // Arrange
        var sourceResponse = """{"data":[]}""";
        var sourceRoot = new SourceRoot { data = new List<ModelData>() };

        var mockCustomModel = new Mock<IModel>();
        mockCustomModel.Setup(m => m.Name).Returns("custom-model");
        _customModels.Add(mockCustomModel.Object);

        _jsonSerializerMock.Setup(j => j.Deserialize<SourceRoot>(sourceResponse))
                          .Returns(sourceRoot);

        OllamaRoot? capturedOllamaRoot = null;
        _jsonSerializerMock.Setup(j => j.Serialize(It.IsAny<OllamaRoot>(), true))
                          .Callback<object, bool>((obj, _) => capturedOllamaRoot = obj as OllamaRoot)
                          .Returns("transformed json");

        // Act
        await _responseTransformer.TransformModelsResponseAsync(sourceResponse, _customModels);

        // Assert
        Assert.NotNull(capturedOllamaRoot);
        Assert.Single(capturedOllamaRoot.models);
        Assert.Equal("custom-model", capturedOllamaRoot.models[0].name);
    }

    [Fact]
    public async Task TransformModelsResponseAsync_WithDeserializationError_ReturnsOriginalContent()
    {
        // Arrange
        var sourceResponse = "invalid json";
        _jsonSerializerMock.Setup(j => j.Deserialize<SourceRoot>(sourceResponse))
                          .Throws(new Exception("Deserialization failed"));

        // Act
        var result = await _responseTransformer.TransformModelsResponseAsync(sourceResponse, _customModels);

        // Assert
        Assert.Equal(sourceResponse, result);
    }

    [Fact]
    public async Task TransformModelsResponseAsync_WithError_LogsError()
    {
        // Arrange
        var sourceResponse = "invalid json";
        _jsonSerializerMock.Setup(j => j.Deserialize<SourceRoot>(sourceResponse))
                          .Throws(new Exception("Deserialization failed"));

        // Act
        await _responseTransformer.TransformModelsResponseAsync(sourceResponse, _customModels);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error transforming models response")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateModelInfoResponseAsync_WithExistingCustomModel_UsesCustomModelName()
    {
        // Arrange
        var modelName = "custom-model";
        var mockCustomModel = new Mock<IModel>();
        mockCustomModel.Setup(m => m.Name).Returns(modelName);
        _customModels.Add(mockCustomModel.Object);

        // Act
        var result = await _responseTransformer.CreateModelInfoResponseAsync(modelName, _customModels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(modelName, result.ModelInfo.Architecture);
        Assert.Contains("chat", result.Capabilities);
    }

    [Fact]
    public async Task CreateModelInfoResponseAsync_WithNonExistingModel_UsesProvidedName()
    {
        // Arrange
        var modelName = "unknown-model";

        // Act
        var result = await _responseTransformer.CreateModelInfoResponseAsync(modelName, _customModels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(modelName, result.ModelInfo.Architecture);
        Assert.Contains("chat", result.Capabilities);
    }

    [Fact]
    public async Task CreateModelInfoResponseAsync_WithNullModelName_UsesUnknown()
    {
        // Act
        var result = await _responseTransformer.CreateModelInfoResponseAsync(null, _customModels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("unknown", result.ModelInfo.Architecture);
        Assert.Contains("chat", result.Capabilities);
    }

    [Fact]
    public async Task CreateModelInfoResponseAsync_CaseInsensitiveModelMatch()
    {
        // Arrange
        var mockCustomModel = new Mock<IModel>();
        mockCustomModel.Setup(m => m.Name).Returns("Custom-Model");
        _customModels.Add(mockCustomModel.Object);

        // Act
        var result = await _responseTransformer.CreateModelInfoResponseAsync("custom-model", _customModels);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Custom-Model", result.ModelInfo.Architecture);
    }
}