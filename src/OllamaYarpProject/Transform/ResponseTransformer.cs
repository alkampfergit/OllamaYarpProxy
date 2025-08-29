using OllamaYarpProject.Interfaces;
using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public class ResponseTransformer : IResponseTransformer
{
    private readonly ILogger<ResponseTransformer> _logger;
    private readonly IStreamingResponseHandler _streamingHandler;
    private readonly IRequestResponseLogger _requestResponseLogger;
    private readonly IStreamingResponseProcessorFactory _processorFactory;
    private readonly IChatProvider _chatProvider;
    private readonly IModelsResponseHandlerFactory _modelsResponseHandlerFactory;

    public ResponseTransformer(
        ILogger<ResponseTransformer> logger,
        IStreamingResponseHandler streamingHandler,
        IRequestResponseLogger requestResponseLogger,
        IStreamingResponseProcessorFactory processorFactory,
        IChatProvider chatProvider,
        IModelsResponseHandlerFactory modelsResponseHandlerFactory)
    {
        _logger = logger;
        _streamingHandler = streamingHandler;
        _requestResponseLogger = requestResponseLogger;
        _processorFactory = processorFactory;
        _chatProvider = chatProvider;
        _modelsResponseHandlerFactory = modelsResponseHandlerFactory;
    }

    public async Task TransformResponseAsync(ResponseTransformContext transformContext)
    {
        var context = transformContext.HttpContext;
        var response = transformContext.ProxyResponse;
        var originalPath = context.Request.Path + context.Request.QueryString;
        var method = context.Request.Method;

        _logger.LogDebug("[RESPONSE TRANSFORM] Processing response for {Method} {OriginalPath}", method, originalPath);

        var requestPath = response?.RequestMessage?.RequestUri?.LocalPath;
        
        if (requestPath == _chatProvider.ModelsEndpoint)
        {
            var handler = _modelsResponseHandlerFactory.GetHandler(_chatProvider);
            await handler.HandleModelsResponseAsync(transformContext);
        }
        else if (requestPath == _chatProvider.ChatCompletionsEndpoint)
        {
            await HandleChatCompletionsResponse(transformContext);
        }

        _logger.LogDebug("[RESPONSE COMPLETE] Response sent for {Method} {OriginalPath}", method, originalPath);
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
                
                // Always handle streaming responses through our handler, regardless of processor availability
                _logger.LogDebug("[RESPONSE TRANSFORM] Using streaming handler for model {ModelName}", modelName);
                await _streamingHandler.HandleStreamingResponseAsync(context, response, requestData);
                transformContext.SuppressResponseBody = true;
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