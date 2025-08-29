using OllamaYarpProject.Interfaces;
using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public class PathTransformer : IPathTransformer
{
    private readonly ILogger<PathTransformer> _logger;
    private readonly IChatProvider _chatProvider;

    public PathTransformer(ILogger<PathTransformer> logger, IChatProvider chatProvider)
    {
        _logger = logger;
        _chatProvider = chatProvider;
    }
    public bool TransformPath(RequestTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;
        
        if (context.Request.Path == "/api/tags")
        {
            // Transform tags endpoint to models
            transformContext.Path = _chatProvider.ModelsEndpoint;
            _logger.LogInformation("[PATH REWRITE] {Method} {OriginalPath} -> {NewPath} (Ollama tags endpoint to models)",
                method, originalPath, "/models");
            return true;
        }
        else if (context.Request.Path == "/v1/chat/completions")
        {
            // Transform OpenAI chat completions to backend format
            transformContext.Path = _chatProvider.ChatCompletionsEndpoint;
            _logger.LogInformation("[PATH REWRITE] {Method} {OriginalPath} -> {NewPath} (OpenAI to backend format)",
                method, originalPath, "/chat/completions");
            return true;
        }

        return false;
    }
}