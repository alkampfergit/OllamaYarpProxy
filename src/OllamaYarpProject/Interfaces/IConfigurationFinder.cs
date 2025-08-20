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
        string? currentDirPath = startDirectory;

        while (currentDirPath != null)
        {
            // Check if directory exists before searching
            if (!Directory.Exists(currentDirPath))
            {
                currentDirPath = _fileSystem.GetParentDirectory(currentDirPath)?.FullName;
                continue;
            }

            // First check for ollama-yarp-proxy.json (new preferred name)
            var ollamaYarpProxyFile = _fileSystem.Combine(currentDirPath, "ollama-yarp-proxy.json");
            if (_fileSystem.Exists(ollamaYarpProxyFile))
            {
                _logger?.LogInformation("Found configuration override file: {ConfigFile} in directory: {Directory}", 
                    ollamaYarpProxyFile, currentDirPath);
                return ollamaYarpProxyFile;
            }

            // Then check for legacy yarpollama* files
            try
            {
                var files = _fileSystem.GetFiles(currentDirPath, "yarpollama*", SearchOption.TopDirectoryOnly);
                var configFile = files.FirstOrDefault(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f.Name), "yarpollama", StringComparison.OrdinalIgnoreCase));

                if (configFile != null)
                {
                    _logger?.LogInformation("Found legacy configuration file: {ConfigFile} in directory: {Directory}", 
                        configFile.FullName, currentDirPath);
                    return configFile.FullName;
                }
            }
            catch (DirectoryNotFoundException)
            {
                // Directory doesn't exist, continue to parent
            }
            catch (UnauthorizedAccessException)
            {
                // No permission to access directory, continue to parent
            }

            currentDirPath = _fileSystem.GetParentDirectory(currentDirPath)?.FullName;
        }

        _logger?.LogDebug("No configuration override file found, using default appsettings.json");
        return null;
    }
}