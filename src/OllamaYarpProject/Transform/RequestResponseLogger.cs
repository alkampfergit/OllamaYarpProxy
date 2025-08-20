using System.Text;

namespace OllamaYarpProject.Transform;

public class RequestResponseLogger : IRequestResponseLogger
{
    private readonly ILogger<RequestResponseLogger> _logger;

    public RequestResponseLogger(ILogger<RequestResponseLogger> logger)
    {
        _logger = logger;
    }

    public async Task WriteRequestResponseToFileAsync(RequestResponseData data)
    {
        try
        {
            // Create filename with timestamp
            var timestamp = data.RequestTime.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"request_{timestamp}.txt";
            var executablePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(executablePath, filename);

            // Prepare content
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

            // Write to file
            await File.WriteAllTextAsync(filePath, content.ToString());
            
            _logger.LogInformation("[FILE WRITE] Request-response data written to: {FilePath}", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError("[FILE WRITE] Error writing request-response file: {Error}", ex.Message);
        }
    }
}