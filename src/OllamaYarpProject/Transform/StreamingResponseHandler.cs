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
        
        if (processor == null)
        {
            _logger.LogInformation("[STREAMING HANDLER] No processor for model {ModelName}, dumping chunks with SSE parsing", modelName);
        }
        else
        {
            _logger.LogInformation("[STREAMING HANDLER] Processing streaming response for model {ModelName} with processor {ProcessorName}", 
                modelName, processor.Name);
        }

        var responseStream = await response.Content.ReadAsStreamAsync();
        var responseBuilder = new StringBuilder();

        // Always process through SSE parsing to dump all chunks, apply processor if available
        await foreach (var sseEvent in _sseParser.ParseSseStreamAsync(responseStream))
        {
            if (sseEvent.IsDone)
            {
                // Write [DONE] event using original format
                responseBuilder.Append(sseEvent.OriginalData);
                var doneBytes = Encoding.UTF8.GetBytes(sseEvent.OriginalData);
                await context.Response.Body.WriteAsync(doneBytes);
                break;
            }

            if (sseEvent.IsJsonData)
            {
                // Parse the chunk
                var chunk = _sseParser.ParseChatCompletionChunk(sseEvent.Data);
                if (chunk != null && processor != null)
                {
                    // We have a processor - process the chunk
                    var modifiedChunk = processor.ProcessChunk(chunk);
                    
                    if (modifiedChunk != null)
                    {
                        // Chunk was modified, serialize the modified version
                        var serializedChunk = _sseParser.SerializeChatCompletionChunk(modifiedChunk);
                        var sseData = $"data: {serializedChunk}\n\n";
                        
                        responseBuilder.Append(sseData);
                        var chunkBytes = Encoding.UTF8.GetBytes(sseData);
                        await context.Response.Body.WriteAsync(chunkBytes);
                    }
                    else
                    {
                        // No modification needed, use original SSE event data
                        responseBuilder.Append(sseEvent.OriginalData);
                        var originalBytes = Encoding.UTF8.GetBytes(sseEvent.OriginalData);
                        await context.Response.Body.WriteAsync(originalBytes);
                    }
                }
                else
                {
                    // No processor or failed to parse, send original SSE event data
                    responseBuilder.Append(sseEvent.OriginalData);
                    var originalBytes = Encoding.UTF8.GetBytes(sseEvent.OriginalData);
                    await context.Response.Body.WriteAsync(originalBytes);
                }
            }
            else
            {
                // Non-JSON data, send original SSE event data as-is
                responseBuilder.Append(sseEvent.OriginalData);
                var eventBytes = Encoding.UTF8.GetBytes(sseEvent.OriginalData);
                await context.Response.Body.WriteAsync(eventBytes);
            }
        }

        // Get final content from processor if available
        if (processor != null)
        {
            var finalContent = processor.GetFinalContent();
            if (!string.IsNullOrEmpty(finalContent))
            {
                var finalBytes = Encoding.UTF8.GetBytes(finalContent);
                await context.Response.Body.WriteAsync(finalBytes);
            }
        }

        // Store response content for logging
        if (requestData != null)
        {
            requestData.ResponseContent = responseBuilder.ToString();
        }

        _logger.LogDebug("[STREAMING HANDLER] Completed streaming response processing for model {ModelName}", modelName);
    }

}