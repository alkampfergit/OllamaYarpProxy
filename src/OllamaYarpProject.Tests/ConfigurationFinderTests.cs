using Microsoft.Extensions.Logging;
using Moq;
using OllamaYarpProject.Interfaces;
using System.IO;
using Xunit;

namespace OllamaYarpProject.Tests;

public class ConfigurationFinderTests
{
    private readonly Mock<IFileSystem> _fileSystemMock;
    private readonly Mock<ILogger<ConfigurationFinder>> _loggerMock;
    private readonly ConfigurationFinder _configurationFinder;

    public ConfigurationFinderTests()
    {
        _fileSystemMock = new Mock<IFileSystem>();
        _loggerMock = new Mock<ILogger<ConfigurationFinder>>();
        _configurationFinder = new ConfigurationFinder(_fileSystemMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void FindYarpOllamaConfig_WithOllamaYarpProxyJson_ReturnsCorrectPath()
    {
        // Arrange
        var testDirectory = @"C:\test";
        var expectedConfigFile = @"C:\test\ollama-yarp-proxy.json";
        
        _fileSystemMock.Setup(fs => fs.Combine(testDirectory, "ollama-yarp-proxy.json"))
                      .Returns(expectedConfigFile);
        _fileSystemMock.Setup(fs => fs.Exists(expectedConfigFile))
                      .Returns(true);

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Equal(expectedConfigFile, result);
        _fileSystemMock.Verify(fs => fs.Exists(expectedConfigFile), Times.Once);
    }

    [Fact]
    public void FindYarpOllamaConfig_WithLegacyYarpollamaFile_ReturnsCorrectPath()
    {
        // Arrange
        var testDirectory = @"C:\test";
        var expectedConfigFile = @"C:\test\yarpollama.json";
        var ollamaYarpProxyFile = @"C:\test\ollama-yarp-proxy.json";

        _fileSystemMock.Setup(fs => fs.Combine(testDirectory, "ollama-yarp-proxy.json"))
                      .Returns(ollamaYarpProxyFile);
        _fileSystemMock.Setup(fs => fs.Exists(ollamaYarpProxyFile))
                      .Returns(false);
        
        var mockFiles = new[]
        {
            new FileInfo(expectedConfigFile)
        };
        
        _fileSystemMock.Setup(fs => fs.GetFiles(testDirectory, "yarpollama*", SearchOption.TopDirectoryOnly))
                      .Returns(mockFiles);

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Equal(expectedConfigFile, result);
    }

    [Fact]
    public void FindYarpOllamaConfig_NoConfigFound_ReturnsNull()
    {
        // Arrange
        var testDirectory = @"C:\test";
        var ollamaYarpProxyFile = @"C:\test\ollama-yarp-proxy.json";

        _fileSystemMock.Setup(fs => fs.Combine(testDirectory, "ollama-yarp-proxy.json"))
                      .Returns(ollamaYarpProxyFile);
        _fileSystemMock.Setup(fs => fs.Exists(ollamaYarpProxyFile))
                      .Returns(false);
        _fileSystemMock.Setup(fs => fs.GetFiles(testDirectory, "yarpollama*", SearchOption.TopDirectoryOnly))
                      .Returns(Array.Empty<FileInfo>());
        _fileSystemMock.Setup(fs => fs.GetParentDirectory(testDirectory))
                      .Returns((DirectoryInfo?)null);

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindYarpOllamaConfig_SearchesParentDirectories()
    {
        // Arrange
        var childDirectory = @"C:\test\child";
        var parentDirectory = @"C:\test";
        var expectedConfigFile = @"C:\test\ollama-yarp-proxy.json";

        // Child directory - no config
        _fileSystemMock.Setup(fs => fs.Combine(childDirectory, "ollama-yarp-proxy.json"))
                      .Returns(@"C:\test\child\ollama-yarp-proxy.json");
        _fileSystemMock.Setup(fs => fs.Exists(@"C:\test\child\ollama-yarp-proxy.json"))
                      .Returns(false);
        _fileSystemMock.Setup(fs => fs.GetFiles(childDirectory, "yarpollama*", SearchOption.TopDirectoryOnly))
                      .Returns(Array.Empty<FileInfo>());
        _fileSystemMock.Setup(fs => fs.GetParentDirectory(childDirectory))
                      .Returns(new DirectoryInfo(parentDirectory));

        // Parent directory - has config
        _fileSystemMock.Setup(fs => fs.Combine(parentDirectory, "ollama-yarp-proxy.json"))
                      .Returns(expectedConfigFile);
        _fileSystemMock.Setup(fs => fs.Exists(expectedConfigFile))
                      .Returns(true);

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(childDirectory);

        // Assert
        Assert.Equal(expectedConfigFile, result);
    }

    [Theory]
    [InlineData("yarpollama")]
    [InlineData("YARPOLLAMA")]
    [InlineData("YarpOllama")]
    public void FindYarpOllamaConfig_CaseInsensitiveLegacyMatch_ReturnsCorrectPath(string fileName)
    {
        // Arrange
        var testDirectory = @"C:\test";
        var expectedConfigFile = $@"C:\test\{fileName}.json";
        var ollamaYarpProxyFile = @"C:\test\ollama-yarp-proxy.json";

        _fileSystemMock.Setup(fs => fs.Combine(testDirectory, "ollama-yarp-proxy.json"))
                      .Returns(ollamaYarpProxyFile);
        _fileSystemMock.Setup(fs => fs.Exists(ollamaYarpProxyFile))
                      .Returns(false);
        
        var mockFiles = new[]
        {
            new FileInfo(expectedConfigFile)
        };
        
        _fileSystemMock.Setup(fs => fs.GetFiles(testDirectory, "yarpollama*", SearchOption.TopDirectoryOnly))
                      .Returns(mockFiles);

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Equal(expectedConfigFile, result);
    }
}