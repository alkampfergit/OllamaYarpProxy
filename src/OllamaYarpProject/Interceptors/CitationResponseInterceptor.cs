using System.Text;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject.Interceptors;

public class CitationResponseInterceptor : IResponseInterceptor
{
    private readonly ILogger<CitationResponseInterceptor> _logger;
    private readonly IChunkManipulatorFactory _chunkManipulatorFactory;

    public string Name => "CitationInterceptor";

    public CitationResponseInterceptor(
        ILogger<CitationResponseInterceptor> logger,
        IChunkManipulatorFactory chunkManipulatorFactory)
    {
        _logger = logger;
        _chunkManipulatorFactory = chunkManipulatorFactory;
    }

    public Task<bool> ShouldInterceptAsync(HttpContext context, HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.ToString();
        var transferEncoding = response.Headers.TransferEncodingChunked;
        bool isStreamingResponse = contentType?.Contains("text/event-stream") == true || 
                                 contentType?.Contains("text/plain") == true ||
                                 transferEncoding == true;

        return Task.FromResult(isStreamingResponse);
    }

    public async Task InterceptAsync(HttpContext context, HttpResponseMessage response)
    {
        var method = context.Request.Method;
        var originalPath = context.Request.Path + context.Request.QueryString;
        
        _logger.LogDebug("[CITATION INTERCEPTOR] Processing streaming response for {Method} {OriginalPath}", method, originalPath);
        
        // Create a new chunk manipulator for this streaming request
        var chunkManipulator = _chunkManipulatorFactory.CreateChunkManipulator();
        
        // For streaming responses, we need to intercept the stream line by line
        var responseStream = await response.Content.ReadAsStreamAsync();
        var reader = new StreamReader(responseStream, Encoding.UTF8);
        var totalChunks = 0;
        var firstChunks = new List<string>();
        var maxFirstChunks = 3;
        
        string? line;
        var currentChunk = new StringBuilder();
        
        while ((line = await reader.ReadLineAsync()) != null)
        {
            // Add the line to current chunk
            currentChunk.AppendLine(line);
            
            // SSE chunks end with empty line (double \n\n)
            if (string.IsNullOrEmpty(line))
            {
                var chunkContent = currentChunk.ToString();
                totalChunks++;
                
                // Capture first few chunks for debug logging
                if (firstChunks.Count < maxFirstChunks)
                {
                    firstChunks.Add(chunkContent);
                }
                
                // Process chunk through ChunkManipulator
                var processedChunk = chunkManipulator.ProcessChunk(chunkContent);
                
                // Only forward to client if we got a processed chunk back (not null)
                if (processedChunk != null)
                {
                    if (processedChunk != chunkContent)
                    {
                        _logger.LogDebug("[CITATION INTERCEPTOR] Chunk {ChunkNumber} was modified by ChunkManipulator", totalChunks);
                    }
                    
                    // Forward the processed chunk to the client
                    var chunkBytes = Encoding.UTF8.GetBytes(processedChunk);
                    await context.Response.Body.WriteAsync(chunkBytes, 0, chunkBytes.Length);
                }
                
                // Reset for next chunk
                currentChunk.Clear();
            }
        }
        
        // Handle any remaining content (chunk without trailing empty line)
        if (currentChunk.Length > 0)
        {
            var remainingContent = currentChunk.ToString();
            
            var processedRemaining = chunkManipulator.ProcessChunk(remainingContent);
            if (processedRemaining != null)
            {
                var remainingBytes = Encoding.UTF8.GetBytes(processedRemaining);
                await context.Response.Body.WriteAsync(remainingBytes, 0, remainingBytes.Length);
            }
        }
        
        // Get final chunk with all accumulated citations
        var finalChunk = chunkManipulator.GetFinalChunk();
        if (finalChunk != null)
        {
            var finalBytes = Encoding.UTF8.GetBytes(finalChunk);
            await context.Response.Body.WriteAsync(finalBytes, 0, finalBytes.Length);
        }
        
        // Log the intercepted response details
        var firstContent = string.Join("", firstChunks);
        var truncatedContent = firstContent.Length > 200 ? firstContent.Substring(0, 200) + "..." : firstContent;
        
        _logger.LogDebug("[CITATION INTERCEPTOR] Streaming response intercepted - Total chunks: {TotalChunks}, First content: {FirstContent}", 
            totalChunks, truncatedContent);
    }
}