using System.IO;

namespace OllamaYarpProject.Interfaces;

public interface IConfigurationFinder
{
    string? FindYarpOllamaConfig(string startDirectory);
}

public class ConfigurationFinder : IConfigurationFinder
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ConfigurationFinder>? _logger;

    public ConfigurationFinder(IFileSystem fileSystem, ILogger<ConfigurationFinder>? logger = null)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public string? FindYarpOllamaConfig(string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);

        while (currentDir != null)
        {
            // First check for ollama-yarp-proxy.json (new preferred name)
            var ollamaYarpProxyFile = _fileSystem.Combine(currentDir.FullName, "ollama-yarp-proxy.json");
            if (_fileSystem.Exists(ollamaYarpProxyFile))
            {
                _logger?.LogInformation("Found configuration override file: {ConfigFile} in directory: {Directory}", 
                    ollamaYarpProxyFile, currentDir.FullName);
                return ollamaYarpProxyFile;
            }

            // Then check for legacy yarpollama* files
            var files = _fileSystem.GetFiles(currentDir.FullName, "yarpollama*", SearchOption.TopDirectoryOnly);
            var configFile = files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f.Name), "yarpollama", StringComparison.OrdinalIgnoreCase));

            if (configFile != null)
            {
                _logger?.LogInformation("Found legacy configuration file: {ConfigFile} in directory: {Directory}", 
                    configFile.FullName, currentDir.FullName);
                return configFile.FullName;
            }

            currentDir = _fileSystem.GetParentDirectory(currentDir.FullName);
        }

        _logger?.LogDebug("No configuration override file found, using default appsettings.json");
        return null;
    }
}