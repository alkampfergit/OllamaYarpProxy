using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ollama;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Helpers;
using OllamaYarpProject.Models;
using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public class RequestTransformer : IRequestTransformer
{
    private readonly ILogger<RequestTransformer> _logger;
    private readonly IModelRouter _modelRouter;
    private readonly IChatProvider _chatProvider;

    public RequestTransformer(ILogger<RequestTransformer> logger, IModelRouter modelRouter, IChatProvider chatProvider)
    {
        _logger = logger;
        _modelRouter = modelRouter;
        _chatProvider = chatProvider;
    }

    public async Task<bool> TransformRequestAsync(RequestTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;

        // Set the Authorization header if JWT token is available for the chat provider
        var jwtToken = _chatProvider.Authentication.JwtToken;
        if (!string.IsNullOrEmpty(jwtToken))
        {
            transformContext.ProxyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
        }

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

        _logger.LogInformation("[DIRECT RESPONSE] {Method} {OriginalPath} -> Sample detail response with model name replacement", method, originalPath);
        
        // Enable buffering to read request body
        context.Request.EnableBuffering();

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        string body = await reader.ReadToEndAsync();

        // Parse the request body to get the model name
        string? requestedModel = null;
        try
        {
            var json = JsonConvert.DeserializeObject(body) as JObject;
            requestedModel = json?.Value<string>("model");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse request body for model name");
            throw new Exception("Request is invalid, we do not have model name");
        }

        var response = transformContext.HttpContext.Response;
        response.StatusCode = 200;
        response.ContentType = "application/json";

        try
        {
            // Read and deserialize the sample-detail.json file
            var sampleDetailPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "sample-detail.json");
            var jsonContent = await File.ReadAllTextAsync(sampleDetailPath);
            
            jsonContent = jsonContent.Replace("{{MODELNAME}}", requestedModel, StringComparison.OrdinalIgnoreCase);
            await response.WriteAsync(jsonContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read or process sample-detail.json file");
            // Fallback to empty response if file not found or parsing fails
            await response.WriteAsync("{}");
        }
        
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