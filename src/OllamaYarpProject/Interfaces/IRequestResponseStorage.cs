using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OllamaYarpProject.Interfaces;

public class RequestResponseData
{
    public string RequestMethod { get; set; } = "";
    public string RequestPath { get; set; } = "";
    public string RequestBody { get; set; } = "";
    public Dictionary<string, string> RequestHeaders { get; set; } = new();
    public DateTime RequestTime { get; set; }
    public string ResponseContent { get; set; } = "";
    public int ResponseStatusCode { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
}

public interface IRequestResponseStorage
{
    Task StoreRequestResponseAsync(RequestResponseData data);
}

public class FileRequestResponseStorage : IRequestResponseStorage
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<FileRequestResponseStorage> _logger;
    private readonly string _basePath;

    public FileRequestResponseStorage(IFileSystem fileSystem, ILogger<FileRequestResponseStorage> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _basePath = AppDomain.CurrentDomain.BaseDirectory;
    }

    public async Task StoreRequestResponseAsync(RequestResponseData data)
    {
        try
        {
            var timestamp = data.RequestTime.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"request_{timestamp}.txt";
            var filePath = _fileSystem.Combine(_basePath, filename);

            var content = new StringBuilder();
            content.AppendLine("=== REQUEST ===");
            content.AppendLine($"Time: {data.RequestTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            content.AppendLine($"Method: {data.RequestMethod}");
            content.AppendLine($"Path: {data.RequestPath}");
            content.AppendLine();
            content.AppendLine("Headers:");
            foreach (var header in data.RequestHeaders)
            {
                content.AppendLine($"  {header.Key}: {header.Value}");
            }
            content.AppendLine();
            content.AppendLine("Body:");
            content.AppendLine(data.RequestBody);
            content.AppendLine();
            content.AppendLine("=== RESPONSE ===");
            content.AppendLine($"Status Code: {data.ResponseStatusCode}");
            content.AppendLine();
            content.AppendLine("Headers:");
            foreach (var header in data.ResponseHeaders)
            {
                content.AppendLine($"  {header.Key}: {header.Value}");
            }
            content.AppendLine();
            content.AppendLine("Content:");
            content.AppendLine(data.ResponseContent);

            await _fileSystem.WriteAllTextAsync(filePath, content.ToString());
            
            _logger.LogInformation("[FILE WRITE] Request-response data written to: {FilePath}", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError("[FILE WRITE] Error writing request-response file: {Error}", ex.Message);
        }
    }
}