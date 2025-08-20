using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ollama;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Helpers;
using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public class RequestTransformer : IRequestTransformer
{
    private readonly ILogger<RequestTransformer> _logger;
    private readonly IModelRouter _modelRouter;

    public RequestTransformer(ILogger<RequestTransformer> logger, IModelRouter modelRouter)
    {
        _logger = logger;
        _modelRouter = modelRouter;
    }

    public async Task<bool> TransformRequestAsync(RequestTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;

        _logger.LogDebug("[REQUEST TRANSFORM] Processing {Method} {OriginalPath}", method, originalPath);

        // Handle chat completions requests with complex logic
        if (context.Request.Path == "/v1/chat/completions")
        {
            return await HandleChatCompletionsRequest(transformContext);
        }

        // Handle show endpoint - direct response
        if (context.Request.Path == "/api/show")
        {
            return await HandleShowRequest(transformContext);
        }

        // Handle version endpoint - direct response  
        if (context.Request.Path == "/api/version")
        {
            return await HandleVersionRequest(transformContext);
        }

        return false;
    }

    private async Task<bool> HandleChatCompletionsRequest(RequestTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;

        // Capture request data for later processing
        var requestData = new RequestResponseData
        {
            RequestMethod = method,
            RequestPath = originalPath,
            RequestTime = DateTime.UtcNow,
            RequestHeaders = context.Request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value.AsEnumerable()))
        };

        // Store in HttpContext for later use
        context.Items["RequestResponseData"] = requestData;

        // Enable buffering to read request body
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        string body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        // Store request body
        requestData.RequestBody = body;

        var cco = JsonConvert.DeserializeObject<GenerateChatCompletionRequest>(body);
        
        // Store model name for later use by processors
        requestData.ModelName = cco?.Model ?? "";

        try
        {
            // Check if the requested model is one of our custom IModel instances
            var customModel = await _modelRouter.GetCustomModelAsync(cco?.Model);

            if (customModel != null)
            {
                _logger.LogInformation("[CUSTOM MODEL] {Method} {OriginalPath} -> Direct response from custom model '{ModelName}'", 
                    method, originalPath, customModel.Name);
                
                var ollamaResponse = cco != null ? await _modelRouter.GenerateDirectResponseAsync(customModel, cco) : null;
                if (ollamaResponse != null)
                {
                    var response = transformContext.HttpContext.Response;
                    response.StatusCode = 200;
                    response.ContentType = "application/json";

                    //serialize to json 
                    var jsonResponse = JsonConvert.SerializeObject(ollamaResponse, Formatting.Indented);
                    await response.WriteAsync(jsonResponse);
                    return true; // Request handled directly
                }
            }
        }
        catch (Exception ex)
        {
            //ignore --- send the request to the proxy
            _logger.LogDebug("[CUSTOM MODEL] Error handling custom model, falling back to proxy: {Error}", ex.Message);
        }

        return false; // Continue with proxy
    }

    private async Task<bool> HandleShowRequest(RequestTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;

        _logger.LogInformation("[DIRECT RESPONSE] {Method} {OriginalPath} -> Mock model info response", method, originalPath);
        
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        string body = await reader.ReadToEndAsync();

        // deserialize in json 
        var json = JsonConvert.DeserializeObject(body) as JObject;
        var model = json?.Value<string>("model");

        // Check if it's one of our custom models
        var customModel = await _modelRouter.GetCustomModelAsync(model);

        var response = transformContext.HttpContext.Response;
        response.StatusCode = 200;
        response.ContentType = "application/json";

        GemmaModel answer = new GemmaModel();
        answer.Capabilities = new List<string> { "chat" };
        answer.ModelInfo = new ModelInfo();
        answer.ModelInfo.Architecture = model ?? "unknown";

        // If it's a custom model, add additional info
        if (customModel != null)
        {
            answer.ModelInfo.Architecture = customModel.Name;
            // Could add more custom model info here if needed
        }

        var jsonResponse = JsonConvert.SerializeObject(answer, Formatting.Indented);
        await response.WriteAsync(jsonResponse);
        
        return true; // Request handled directly
    }

    private async Task<bool> HandleVersionRequest(RequestTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;

        _logger.LogInformation("[DIRECT RESPONSE] {Method} {OriginalPath} -> Static version response", method, originalPath);
        
        var response = transformContext.HttpContext.Response;
        response.StatusCode = 200;
        response.ContentType = "application/json";
        await response.WriteAsync("{\"version\": \"0.9.6\"}");
        
        return true; // Request handled directly
    }
}