using System.Text;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Services;

namespace OllamaYarpProject.Transform;

public class StreamingResponseHandler : IStreamingResponseHandler
{
    private readonly ILogger<StreamingResponseHandler> _logger;
    private readonly ISseParser _sseParser;
    private readonly IStreamingResponseProcessorFactory _processorFactory;

    public StreamingResponseHandler(
        ILogger<StreamingResponseHandler> logger,
        ISseParser sseParser,
        IStreamingResponseProcessorFactory processorFactory)
    {
        _logger = logger;
        _sseParser = sseParser;
        _processorFactory = processorFactory;
    }

    public async Task HandleStreamingResponseAsync(HttpContext context, HttpResponseMessage response, RequestResponseData? requestData)
    {
        var modelName = requestData?.ModelName ?? "";
        var processor = _processorFactory.GetProcessor(modelName);
        
        // If no processor is configured, use fast-path without deserialization
        if (processor == null)
        {
            _logger.LogDebug("[STREAMING HANDLER] No processor for model {ModelName}, using fast-path passthrough", modelName);
            await PassThroughStreamingResponse(context, response, requestData);
            return;
        }
        
        _logger.LogDebug("[STREAMING HANDLER] Processing streaming response for model {ModelName} with processor {ProcessorName}", 
            modelName, processor.Name);

        var responseStream = await response.Content.ReadAsStreamAsync();
        var responseBuilder = new StringBuilder();

        await foreach (var sseEvent in _sseParser.ParseSseStreamAsync(responseStream))
        {
            if (sseEvent.IsDone)
            {
                // Write [DONE] event
                var doneEventData = "data: [DONE]\n\n";
                responseBuilder.Append(doneEventData);
                var doneBytes = Encoding.UTF8.GetBytes(doneEventData);
                await context.Response.Body.WriteAsync(doneBytes, 0, doneBytes.Length);
                break;
            }

            if (sseEvent.IsJsonData)
            {
                // Parse the chunk
                var chunk = _sseParser.ParseChatCompletionChunk(sseEvent.Data);
                if (chunk != null)
                {
                    // Process the chunk if we have a processor
                    var modifiedChunk = processor?.ProcessChunk(chunk);
                    var chunkToSend = modifiedChunk ?? chunk;

                    // Serialize back to SSE format
                    var serializedChunk = _sseParser.SerializeChatCompletionChunk(chunkToSend);
                    var sseData = $"data: {serializedChunk}\n\n";
                    
                    responseBuilder.Append(sseData);
                    var chunkBytes = Encoding.UTF8.GetBytes(sseData);
                    await context.Response.Body.WriteAsync(chunkBytes, 0, chunkBytes.Length);
                }
                else
                {
                    // Failed to parse, send original
                    var originalData = $"data: {sseEvent.Data}\n\n";
                    responseBuilder.Append(originalData);
                    var originalBytes = Encoding.UTF8.GetBytes(originalData);
                    await context.Response.Body.WriteAsync(originalBytes, 0, originalBytes.Length);
                }
            }
            else
            {
                // Non-JSON data, send as-is
                var eventData = $"data: {sseEvent.Data}\n\n";
                responseBuilder.Append(eventData);
                var eventBytes = Encoding.UTF8.GetBytes(eventData);
                await context.Response.Body.WriteAsync(eventBytes, 0, eventBytes.Length);
            }
        }

        // Get final content from processor
        var finalContent = processor?.GetFinalContent();
        if (!string.IsNullOrEmpty(finalContent))
        {
            var finalBytes = Encoding.UTF8.GetBytes(finalContent);
            await context.Response.Body.WriteAsync(finalBytes, 0, finalBytes.Length);
        }

        // Store response content for logging
        if (requestData != null)
        {
            requestData.ResponseContent = responseBuilder.ToString();
        }

        _logger.LogDebug("[STREAMING HANDLER] Completed streaming response processing for model {ModelName}", modelName);
    }

    private async Task PassThroughStreamingResponse(HttpContext context, HttpResponseMessage response, RequestResponseData? requestData)
    {
        var responseStream = await response.Content.ReadAsStreamAsync();
        var responseBuilder = new StringBuilder();
        
        // Simple pass-through without any deserialization or processing
        using var reader = new StreamReader(responseStream, Encoding.UTF8);
        var buffer = new char[8192]; // 8KB buffer
        int bytesRead;
        
        while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var chunk = new string(buffer, 0, bytesRead);
            responseBuilder.Append(chunk);
            
            var chunkBytes = Encoding.UTF8.GetBytes(chunk);
            await context.Response.Body.WriteAsync(chunkBytes);
            
            // Flush immediately to maintain streaming behavior
            await context.Response.Body.FlushAsync();
        }
        
        // Store response content for logging
        if (requestData != null)
        {
            requestData.ResponseContent = responseBuilder.ToString();
        }
        
        _logger.LogDebug("[STREAMING HANDLER] Completed fast-path streaming response passthrough");
    }
}