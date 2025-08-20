using Microsoft.Extensions.Logging;
using Moq;
using OllamaYarpProject.Interfaces;
using System.IO;
using Xunit;

namespace OllamaYarpProject.Tests;

public class ConfigurationFinderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly ConfigurationFinder _configurationFinder;
    private readonly List<string> _createdDirectories = new();
    private readonly List<string> _createdFiles = new();

    public ConfigurationFinderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "OllamaYarpTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _createdDirectories.Add(_tempDirectory);

        var fileSystem = new FileSystemWrapper();
        var logger = Mock.Of<ILogger<ConfigurationFinder>>();
        _configurationFinder = new ConfigurationFinder(fileSystem, logger);
    }

    public void Dispose()
    {
        // Clean up created files
        foreach (var file in _createdFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up created directories (in reverse order)
        for (int i = _createdDirectories.Count - 1; i >= 0; i--)
        {
            try
            {
                if (Directory.Exists(_createdDirectories[i]))
                    Directory.Delete(_createdDirectories[i], true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempFile(string directory, string fileName)
    {
        var filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, "{}"); // Create empty JSON file
        _createdFiles.Add(filePath);
        return filePath;
    }

    private string CreateTempDirectory(string parentDirectory, string dirName)
    {
        var dirPath = Path.Combine(parentDirectory, dirName);
        Directory.CreateDirectory(dirPath);
        _createdDirectories.Add(dirPath);
        return dirPath;
    }

    [Fact]
    public void FindYarpOllamaConfig_WithOllamaYarpProxyJson_ReturnsCorrectPath()
    {
        // Arrange
        var testDirectory = CreateTempDirectory(_tempDirectory, "test");
        var expectedConfigFile = CreateTempFile(testDirectory, "ollama-yarp-proxy.json");

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Equal(expectedConfigFile, result);
    }

    [Fact]
    public void FindYarpOllamaConfig_WithLegacyYarpollamaFile_ReturnsCorrectPath()
    {
        // Arrange
        var testDirectory = CreateTempDirectory(_tempDirectory, "test");
        var expectedConfigFile = CreateTempFile(testDirectory, "yarpollama.json");

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Equal(expectedConfigFile, result);
    }

    [Fact]
    public void FindYarpOllamaConfig_NoConfigFound_ReturnsNull()
    {
        // Arrange
        var testDirectory = CreateTempDirectory(_tempDirectory, "test");
        // Don't create any config files

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void FindYarpOllamaConfig_SearchesParentDirectories()
    {
        // Arrange
        var parentDirectory = CreateTempDirectory(_tempDirectory, "parent");
        var childDirectory = CreateTempDirectory(parentDirectory, "child");
        var expectedConfigFile = CreateTempFile(parentDirectory, "ollama-yarp-proxy.json");

        // Act - Start search from child directory
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
        var testDirectory = CreateTempDirectory(_tempDirectory, "test");
        var expectedConfigFile = CreateTempFile(testDirectory, $"{fileName}.json");

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert
        Assert.Equal(expectedConfigFile, result);
    }

    [Fact]
    public void FindYarpOllamaConfig_PrefersOllamaYarpProxyOverLegacy()
    {
        // Arrange
        var testDirectory = CreateTempDirectory(_tempDirectory, "test");
        var legacyFile = CreateTempFile(testDirectory, "yarpollama.json");
        var preferredFile = CreateTempFile(testDirectory, "ollama-yarp-proxy.json");

        // Act
        var result = _configurationFinder.FindYarpOllamaConfig(testDirectory);

        // Assert - Should prefer ollama-yarp-proxy.json over legacy file
        Assert.Equal(preferredFile, result);
    }

    [Fact]
    public void FindYarpOllamaConfig_WithNonExistentDirectory_ReturnsNull()
    {
        // Arrange
        var nonExistentDirectory = Path.Combine(_tempDirectory, "does-not-exist");

        // Act & Assert - Should not throw and should return null
        var result = _configurationFinder.FindYarpOllamaConfig(nonExistentDirectory);
        Assert.Null(result);
    }
}