using System;
using System.IO;
using System.Threading.Tasks;

namespace OllamaYarpProject.Interfaces;

public interface IFileSystem
{
    Task WriteAllTextAsync(string path, string content);
    bool Exists(string path);
    FileInfo[] GetFiles(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly);
    DirectoryInfo? GetParentDirectory(string path);
    string GetFullPath(string path);
    string Combine(params string[] paths);
}

public class FileSystemWrapper : IFileSystem
{
    public async Task WriteAllTextAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content);
    }

    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public FileInfo[] GetFiles(string path, string searchPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var directory = new DirectoryInfo(path);
        return directory.GetFiles(searchPattern, searchOption);
    }

    public DirectoryInfo? GetParentDirectory(string path)
    {
        var directory = new DirectoryInfo(path);
        return directory.Parent;
    }

    public string GetFullPath(string path)
    {
        return Path.GetFullPath(path);
    }

    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }
}