namespace OllamaYarpProject.Interfaces;

public interface IPathRewriter
{
    string? RewritePath(string originalPath, string method);
    bool ShouldRewrite(string path);
}

public class StandardPathRewriter : IPathRewriter
{
    private readonly ILogger<StandardPathRewriter> _logger;

    public StandardPathRewriter(ILogger<StandardPathRewriter> logger)
    {
        _logger = logger;
    }

    public string? RewritePath(string originalPath, string method)
    {
        if (originalPath == "/api/tags")
        {
            _logger.LogInformation("[PATH REWRITE] {Method} {OriginalPath} -> {NewPath} (Ollama tags endpoint to models)", 
                method, originalPath, "/models");
            return "/models";
        }
        else if (originalPath == "/v1/chat/completions")
        {
            _logger.LogInformation("[PATH REWRITE] {Method} {OriginalPath} -> {NewPath} (OpenAI to backend format)", 
                method, originalPath, "/chat/completions");
            return "/chat/completions";
        }
        
        return null; // No rewrite needed
    }

    public bool ShouldRewrite(string path)
    {
        return path switch
        {
            "/api/tags" => true,
            "/v1/chat/completions" => true,
            _ => false
        };
    }
}