using OllamaYarpProject.Models;
using Xunit;

namespace OllamaYarpProject.Tests;

public class O3ProConfigTests
{
    [Fact]
    public void O3ProConfig_DefaultConstructor_SetsDefaultValues()
    {
        // Act
        var config = new O3ProConfig();

        // Assert
        Assert.Equal(string.Empty, config.Endpoint);
        Assert.Equal(string.Empty, config.ApiKey);
        Assert.Equal("o3-pro", config.DeploymentName);
    }

    [Fact]
    public void O3ProConfig_CanSetProperties()
    {
        // Arrange
        var config = new O3ProConfig();
        var endpoint = "https://test.openai.azure.com/";
        var apiKey = "test-api-key";
        var deploymentName = "custom-o3-pro";

        // Act
        config.Endpoint = endpoint;
        config.ApiKey = apiKey;
        config.DeploymentName = deploymentName;

        // Assert
        Assert.Equal(endpoint, config.Endpoint);
        Assert.Equal(apiKey, config.ApiKey);
        Assert.Equal(deploymentName, config.DeploymentName);
    }

    [Fact]
    public void O3ProConfig_PropertiesAllowNullAssignment()
    {
        // Arrange
        var config = new O3ProConfig
        {
            Endpoint = "test",
            ApiKey = "test",
            DeploymentName = "test"
        };

        // Act & Assert (should not throw)
        config.Endpoint = null!;
        config.ApiKey = null!;
        config.DeploymentName = null!;
        
        Assert.Null(config.Endpoint);
        Assert.Null(config.ApiKey);
        Assert.Null(config.DeploymentName);
    }
}