using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject;

public interface IChunkManipulatorFactory
{
    IChunkManipulator CreateChunkManipulator();
}

public interface IChunkManipulator
{
    string? ProcessChunk(string chunk);
    string? GetFinalChunk();
}


public class ChunkManipulatorFactory : IChunkManipulatorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ChunkManipulatorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IChunkManipulator CreateChunkManipulator()
    {
        return _serviceProvider.GetRequiredService<ChunkManipulator>();
    }
}

public class ChunkManipulator : IChunkManipulator
{
    private readonly ILogger<ChunkManipulator> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly Regex CitationPattern = new(@"\[(\d+)\]", RegexOptions.Compiled);

    private readonly StringBuilder _accumulatedContent = new();
    private readonly HashSet<string> _allCitations = new(StringComparer.OrdinalIgnoreCase);

    public ChunkManipulator(ILogger<ChunkManipulator> logger)
    {
        _logger = logger;
    }

    public string? ProcessChunk(string chunk)
    {
        try
        {
            // Check if chunk starts with "data: " and contains JSON
            if (!chunk.StartsWith("data: ") || chunk.Trim() == "data: [DONE]")
            {
                _accumulatedContent.Append(chunk);
                return HasCarriageReturn(chunk) ? FlushAccumulatedContent() : null;
            }

            var jsonPart = chunk.Substring(6).Trim(); // Remove "data: " prefix
            if (string.IsNullOrEmpty(jsonPart))
            {
                _accumulatedContent.Append(chunk);
                return HasCarriageReturn(chunk) ? FlushAccumulatedContent() : null;
            }

            // Deserialize JSON into ChatCompletionChunk
            var chunkData = JsonSerializer.Deserialize<ChatCompletionChunk>(jsonPart, JsonOptions);
            
            if (chunkData == null)
            {
                _logger.LogDebug("[CHUNK MANIPULATOR] Failed to deserialize chunk JSON");
                _accumulatedContent.Append(chunk);
                return HasCarriageReturn(chunk) ? FlushAccumulatedContent() : null;
            }

            _logger.LogDebug("[CHUNK MANIPULATOR] Successfully parsed chunk with {ChoicesCount} choices and {CitationsCount} citations",
                chunkData.Choices?.Count ?? 0, chunkData.Citations?.Count ?? 0);

            // Collect citations from this chunk
            if (chunkData.Citations != null && chunkData.Citations.Count > 0)
            {
                foreach (var citation in chunkData.Citations)
                {
                    if (!string.IsNullOrEmpty(citation))
                    {
                        _allCitations.Add(citation);
                    }
                }
            }

            // Extract content from chunk and accumulate it
            var contentFromChunk = ExtractContentFromChunk(chunkData);
            _accumulatedContent.Append(contentFromChunk);

            // Check if we have a carriage return in the accumulated content
            return HasCarriageReturn(_accumulatedContent.ToString()) ? FlushAccumulatedContent() : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("[CHUNK MANIPULATOR] JSON parsing error: {Error}", ex.Message);
            _accumulatedContent.Append(chunk);
            return HasCarriageReturn(chunk) ? FlushAccumulatedContent() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError("[CHUNK MANIPULATOR] Unexpected error processing chunk: {Error}", ex.Message);
            _accumulatedContent.Append(chunk);
            return HasCarriageReturn(chunk) ? FlushAccumulatedContent() : null;
        }
    }

    public string? GetFinalChunk()
    {
        if (_accumulatedContent.Length == 0)
        {
            return null;
        }

        var finalContent = _accumulatedContent.ToString();
        
        // Process citations in the final accumulated content
        finalContent = ProcessCitationsInContent(finalContent);
        
        // Add all collected citations at the end
        if (_allCitations.Count > 0)
        {
            var citationLinks = _allCitations
                .Select((citation, index) => $"[{index + 1}]({citation})")
                .ToList();
            
            finalContent += "\n\nCit: " + string.Join(", ", citationLinks);
        }

        _logger.LogInformation("[CHUNK MANIPULATOR] Final chunk with {CitationCount} citations", _allCitations.Count);
        return finalContent;
    }

    private bool HasCarriageReturn(string content)
    {
        return content.Contains('\n') || content.Contains('\r');
    }

    private string FlushAccumulatedContent()
    {
        var content = _accumulatedContent.ToString();
        
        // Find the position of the carriage return
        var crIndex = Math.Max(content.IndexOf('\n'), content.IndexOf('\r'));
        if (crIndex >= 0)
        {
            // Return content up to and including the carriage return
            var contentToReturn = content.Substring(0, crIndex + 1);
            
            // Process citations in this content
            contentToReturn = ProcessCitationsInContent(contentToReturn);
            
            // Keep the rest in the accumulator
            _accumulatedContent.Clear();
            if (crIndex + 1 < content.Length)
            {
                _accumulatedContent.Append(content.Substring(crIndex + 1));
            }
            
            return contentToReturn;
        }

        // If no carriage return found, return all accumulated content
        var result = ProcessCitationsInContent(content);
        _accumulatedContent.Clear();
        return result;
    }

    private string ExtractContentFromChunk(ChatCompletionChunk chunkData)
    {
        var content = new StringBuilder();
        
        if (chunkData.Choices != null)
        {
            foreach (var choice in chunkData.Choices)
            {
                if (choice?.Delta?.Content != null)
                {
                    content.Append(choice.Delta.Content);
                }
            }
        }
        
        return content.ToString();
    }

    private string ProcessCitationsInContent(string content)
    {
        return ProcessCitationsInContent(content, _allCitations.ToList());
    }

    private string ProcessCitationsInContent(string content, List<string> citations)
    {
        var matches = CitationPattern.Matches(content);
        if (matches.Count == 0)
        {
            return content;
        }

        // Group all citation numbers found in the content
        var citationNumbers = new List<int>();
        foreach (Match match in matches)
        {
            var citationNumber = int.Parse(match.Groups[1].Value);
            if (!citationNumbers.Contains(citationNumber))
            {
                citationNumbers.Add(citationNumber);
            }
        }

        // Sort citation numbers to maintain consistent order
        citationNumbers.Sort();

        // Build the citation expansion text
        var citationLinks = new List<string>();
        foreach (var citationNumber in citationNumbers)
        {
            var citationIndex = citationNumber - 1; // Convert to 0-based index
            
            if (citationIndex >= 0 && citationIndex < citations.Count)
            {
                var citationUrl = citations[citationIndex];
                if (!string.IsNullOrEmpty(citationUrl))
                {
                    citationLinks.Add($"[{citationNumber}]({citationUrl})");
                    _logger.LogDebug("[CHUNK MANIPULATOR] Added citation {CitationNumber} to expansion: {Url}",
                        citationNumber, citationUrl);
                }
            }
            else
            {
                _logger.LogWarning("[CHUNK MANIPULATOR] Citation reference {CitationNumber} not found in citations array (count: {CitationCount})",
                    citationNumber, citations.Count);
            }
        }

        if (citationLinks.Count == 0)
        {
            return content;
        }

        // Create the citation expansion text
        var citationExpansion = "Cit: " + string.Join(", ", citationLinks);

        // Replace all citation patterns with empty string and append the expansion
        var modifiedContent = CitationPattern.Replace(content, "");
        modifiedContent += citationExpansion;

        _logger.LogDebug("[CHUNK MANIPULATOR] Replaced {CitationCount} citation patterns with expansion: {CitationExpansion}",
            matches.Count, citationExpansion);

        return modifiedContent;
    }
}