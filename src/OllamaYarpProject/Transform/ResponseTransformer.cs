using System.Text;
using Newtonsoft.Json;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Services;
using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public class ResponseTransformer : IResponseTransformer
{
    private readonly ILogger<ResponseTransformer> _logger;
    private readonly IModelRouter _modelRouter;
    private readonly IStreamingResponseHandler _streamingHandler;
    private readonly IRequestResponseLogger _requestResponseLogger;
    private readonly IStreamingResponseProcessorFactory _processorFactory;

    public ResponseTransformer(
        ILogger<ResponseTransformer> logger,
        IModelRouter modelRouter,
        IStreamingResponseHandler streamingHandler,
        IRequestResponseLogger requestResponseLogger,
        IStreamingResponseProcessorFactory processorFactory)
    {
        _logger = logger;
        _modelRouter = modelRouter;
        _streamingHandler = streamingHandler;
        _requestResponseLogger = requestResponseLogger;
        _processorFactory = processorFactory;
    }

    public async Task TransformResponseAsync(ResponseTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var response = transformContext.ProxyResponse;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;
        
        _logger.LogDebug("[RESPONSE TRANSFORM] Processing response for {Method} {OriginalPath}", method, originalPath);
        
        if (response?.RequestMessage?.RequestUri?.LocalPath == "/models")
        {
            await HandleModelsResponse(transformContext);
        }
        else if (response?.RequestMessage?.RequestUri?.LocalPath == "/chat/completions")
        {
            await HandleChatCompletionsResponse(transformContext);
        }

        _logger.LogDebug("[RESPONSE COMPLETE] Response sent for {Method} {OriginalPath}", method, originalPath);
    }

    private async Task HandleModelsResponse(ResponseTransformContext transformContext)
    {
        var response = transformContext.ProxyResponse;
        
        _logger.LogInformation("[RESPONSE TRANSFORM] Transforming /models response from backend to Ollama format");
        
        //I need to grab the original content and then change the schema
        var content = await response!.Content.ReadAsStringAsync();
        var source = JsonConvert.DeserializeObject<SourceRoot>(content);
        
        _logger.LogDebug("[RESPONSE TRANSFORM] Backend returned {ModelCount} models", source?.data?.Count ?? 0);

        // Create models from the proxy response
        var proxyModels = source!.data
            .Select(m => new OllamaModel
            {
                name = m.id,
                model = m.id,
                modified_at = "2024-02-24T18:29:19.5508829+01:00",
                size = 1966917458,
                digest = Guid.NewGuid().ToString(),
            });

        // Add IModel instances to the list
        var customModels = _modelRouter.GetModels().Select(m => new OllamaModel
        {
            name = m.Name,
            model = m.Name,
            modified_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"),
            size = 0, // Custom models don't have a size
            digest = Guid.NewGuid().ToString(),
        });

        var ollamaModels = new OllamaRoot
        {
            models = proxyModels.Concat(customModels).ToList()
        };
        var ollamaJson = JsonConvert.SerializeObject(ollamaModels, Formatting.Indented);

        transformContext.SuppressResponseBody = true;

        // Convert modified JSON to bytes
        var modifiedBytes = Encoding.UTF8.GetBytes(ollamaJson);

        // Update the Content-Length header to match the new content
        transformContext.HttpContext.Response.ContentLength = modifiedBytes.Length;

        // Set the correct content type
        transformContext.HttpContext.Response.ContentType = "application/json";

        // Write the modified content
        await transformContext.HttpContext.Response.Body.WriteAsync(modifiedBytes);
    }

    private async Task HandleChatCompletionsResponse(ResponseTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var response = transformContext.ProxyResponse;
        
        // Get stored request data to determine the model
        RequestResponseData? requestData = null;
        if (context.Items.TryGetValue("RequestResponseData", out var requestDataObj) 
            && requestDataObj is RequestResponseData data)
        {
            requestData = data;
            requestData.ResponseStatusCode = (int)response!.StatusCode;
            requestData.ResponseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value.AsEnumerable()));
        }
        
        try
        {
            var contentType = response!.Content.Headers.ContentType?.ToString();
            var transferEncoding = response.Headers.TransferEncodingChunked;
            bool isStreamingResponse = contentType?.Contains("text/event-stream") == true || 
                                     contentType?.Contains("text/plain") == true ||
                                     transferEncoding == true;

            if (isStreamingResponse)
            {
                var modelName = requestData?.ModelName ?? "";
                var processor = _processorFactory.GetProcessor(modelName);
                
                if (processor != null)
                {
                    // Handle streaming response with processor
                    _logger.LogDebug("[RESPONSE TRANSFORM] Using streaming handler for model {ModelName} with processor {ProcessorName}", 
                        modelName, processor.Name);
                    await _streamingHandler.HandleStreamingResponseAsync(context, response, requestData);
                    transformContext.SuppressResponseBody = true;
                }
                else
                {
                    // No processor needed - let YARP handle the response directly without any custom processing
                    _logger.LogDebug("[RESPONSE TRANSFORM] No processor for model {ModelName}, letting YARP handle streaming response directly", 
                        modelName);
                    
                    // Store response data for logging if needed
                    if (requestData != null)
                    {
                        // For logging purposes, we can still capture some response info without processing the stream
                        requestData.ResponseContent = "[Streaming response - passed through directly]";
                    }
                    
                    // Don't suppress response body - let YARP stream it directly to the client
                }
            }
            else
            {
                // Handle non-streaming response
                var content = await response.Content.ReadAsStringAsync();
                var contentLength = content.Length;
                
                if (requestData != null)
                {
                    requestData.ResponseContent = content;
                }
                
                var lines = content.Split('\n');
                var firstLines = string.Join("\n", lines.Take(3));
                var truncatedContent = firstLines.Length > 200 ? firstLines.Substring(0, 200) + "..." : firstLines;
                
                _logger.LogDebug("[RESPONSE INTERCEPT] Non-streaming response. Total length: {ContentLength} chars, First lines: {FirstContent}", 
                    contentLength, truncatedContent);
            }
            
            // Write request-response data to file
            if (requestData != null)
            {
                await _requestResponseLogger.WriteRequestResponseToFileAsync(requestData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[RESPONSE INTERCEPT] Error intercepting response: {Error}", ex.Message);
        }
    }
}