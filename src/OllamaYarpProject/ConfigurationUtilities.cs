namespace OllamaYarpProject;

public static class ConfigurationUtilities
{
    public static string? FindYarpOllamaConfig(string startDirectory, ILogger? logger = null)
    {
        var currentDir = new DirectoryInfo(startDirectory);

        while (currentDir != null)
        {
            // First check for ollama-yarp-proxy.json (new preferred name)
            var ollamaYarpProxyFile = Path.Combine(currentDir.FullName, "ollama-yarp-proxy.json");
            if (File.Exists(ollamaYarpProxyFile))
            {
                logger?.LogInformation("Found configuration override file: {ConfigFile} in directory: {Directory}", 
                    ollamaYarpProxyFile, currentDir.FullName);
                return ollamaYarpProxyFile;
            }

            // Then check for legacy yarpollama* files
            var files = currentDir.GetFiles("yarpollama*", SearchOption.TopDirectoryOnly);
            var configFile = files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f.Name), "yarpollama", StringComparison.OrdinalIgnoreCase));

            if (configFile != null)
            {
                logger?.LogInformation("Found legacy configuration file: {ConfigFile} in directory: {Directory}", 
                    configFile.FullName, currentDir.FullName);
                return configFile.FullName;
            }

            currentDir = currentDir.Parent;
        }

        logger?.LogDebug("No configuration override file found, using default appsettings.json");
        return null;
    }
}