namespace OllamaYarpProject;

public static class ConfigurationUtilities
{
    public static string? FindYarpOllamaConfig(string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);

        while (currentDir != null)
        {
            var files = currentDir.GetFiles("yarpollama*", SearchOption.TopDirectoryOnly);
            var configFile = files.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f.Name), "yarpollama", StringComparison.OrdinalIgnoreCase));

            if (configFile != null)
            {
                return configFile.FullName;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }
}