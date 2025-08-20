using System.Text;
using System.Text.RegularExpressions;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject.Processors;

public class CitationStreamingProcessor : IStreamingResponseProcessor
{
    private readonly ILogger<CitationStreamingProcessor> _logger;
    private static readonly Regex CitationPattern = new(@"\[(\d+)\]", RegexOptions.Compiled);

    private readonly StringBuilder _accumulatedContent = new();
    private readonly HashSet<string> _allCitations = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "CitationInterceptor"; // Keep same name for backward compatibility

    public CitationStreamingProcessor(ILogger<CitationStreamingProcessor> logger)
    {
        _logger = logger;
    }

    public bool ShouldProcess(HttpContext context, string modelName)
    {
        // This processor should only be used when explicitly configured
        // Return false here to prevent it from being selected as a fallback
        // The factory will only select this processor if configured in ModelInterceptorMappings
        return false;
    }

    public Interfaces.ChatCompletionChunk? ProcessChunk(Interfaces.ChatCompletionChunk chunk)
    {
        try
        {
            // Collect citations from this chunk
            if (chunk.Citations != null && chunk.Citations.Count > 0)
            {
                foreach (var citation in chunk.Citations)
                {
                    if (!string.IsNullOrEmpty(citation))
                    {
                        _allCitations.Add(citation);
                    }
                }
            }

            // Extract and accumulate content from chunk
            var contentFromChunk = ExtractContentFromChunk(chunk);
            if (!string.IsNullOrEmpty(contentFromChunk))
            {
                _accumulatedContent.Append(contentFromChunk);
                
                // Process citations in the content and remove citation markers
                var processedContent = ProcessCitationsInContent(contentFromChunk);
                
                // If content was modified, update the chunk
                if (processedContent != contentFromChunk)
                {
                    UpdateChunkContent(chunk, processedContent);
                    _logger.LogDebug("[CITATION PROCESSOR] Processed content with citations");
                    return chunk;
                }
            }

            // Return null to indicate no modification needed
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError("[CITATION PROCESSOR] Error processing chunk: {Error}", ex.Message);
            return null;
        }
    }

    public string? GetFinalContent()
    {
        if (_allCitations.Count == 0)
        {
            return null;
        }

        // Generate final citation links
        var citationLinks = _allCitations
            .Select((citation, index) => $"[{index + 1}]({citation})")
            .ToList();
        
        var finalContent = "\n\nCit: " + string.Join(", ", citationLinks);
        
        _logger.LogInformation("[CITATION PROCESSOR] Generated final content with {CitationCount} citations", _allCitations.Count);
        return finalContent;
    }

    private string ExtractContentFromChunk(Interfaces.ChatCompletionChunk chunk)
    {
        var content = new StringBuilder();
        
        if (chunk.Choices != null)
        {
            foreach (var choice in chunk.Choices)
            {
                if (choice?.Delta?.Content != null)
                {
                    content.Append(choice.Delta.Content);
                }
            }
        }
        
        return content.ToString();
    }

    private void UpdateChunkContent(Interfaces.ChatCompletionChunk chunk, string newContent)
    {
        if (chunk.Choices != null)
        {
            foreach (var choice in chunk.Choices)
            {
                if (choice?.Delta != null)
                {
                    choice.Delta.Content = newContent;
                    break; // Typically only update the first choice
                }
            }
        }
    }

    private string ProcessCitationsInContent(string content)
    {
        var matches = CitationPattern.Matches(content);
        if (matches.Count == 0)
        {
            return content;
        }

        // Remove citation markers from content
        var modifiedContent = CitationPattern.Replace(content, "");
        
        _logger.LogDebug("[CITATION PROCESSOR] Removed {CitationCount} citation markers from content", matches.Count);
        
        return modifiedContent;
    }
}