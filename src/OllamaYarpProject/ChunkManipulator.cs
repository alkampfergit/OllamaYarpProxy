using System.Text.Json;
using System.Text.RegularExpressions;

namespace OllamaYarpProject;

public class ChatCompletionChunk
{
    public string Id { get; set; } = "";
    public long Created { get; set; }
    public string Model { get; set; } = "";
    public string Object { get; set; } = "";
    public List<Choice> Choices { get; set; } = new();
    public List<string> Citations { get; set; } = new();
}

public class Choice
{
    public int Index { get; set; }
    public Delta Delta { get; set; } = new();
}

public class Delta
{
    public string Content { get; set; } = "";
}

public class ChunkManipulator
{
    private readonly ILogger<ChunkManipulator> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly Regex CitationPattern = new(@"\[(\d+)\]", RegexOptions.Compiled);

    public ChunkManipulator(ILogger<ChunkManipulator> logger)
    {
        _logger = logger;
    }

    public string ProcessChunk(string chunk)
    {
        try
        {
            // Check if chunk starts with "data: " and contains JSON
            if (!chunk.StartsWith("data: ") || chunk.Trim() == "data: [DONE]")
            {
                return chunk;
            }

            var jsonPart = chunk.Substring(6).Trim(); // Remove "data: " prefix
            if (string.IsNullOrEmpty(jsonPart))
            {
                return chunk;
            }

            // Deserialize JSON into ChatCompletionChunk
            var chunkData = JsonSerializer.Deserialize<ChatCompletionChunk>(jsonPart, JsonOptions);
            
            if (chunkData == null)
            {
                _logger.LogDebug("[CHUNK MANIPULATOR] Failed to deserialize chunk JSON");
                return chunk;
            }

            _logger.LogDebug("[CHUNK MANIPULATOR] Successfully parsed chunk with {ChoicesCount} choices and {CitationsCount} citations",
                chunkData.Choices?.Count ?? 0, chunkData.Citations?.Count ?? 0);

            // Process citations if both choices and citations exist
            if (chunkData.Choices != null && chunkData.Citations != null && chunkData.Citations.Count > 0)
            {
                bool modified = false;

                foreach (var choice in chunkData.Choices)
                {
                    if (choice?.Delta?.Content != null)
                    {
                        var originalContent = choice.Delta.Content;
                        var modifiedContent = ProcessCitationsInContent(originalContent, chunkData.Citations);
                        
                        if (modifiedContent != originalContent)
                        {
                            choice.Delta.Content = modifiedContent;
                            modified = true;
                            _logger.LogDebug("[CHUNK MANIPULATOR] Replaced citations in choice {ChoiceIndex}: '{Original}' -> '{Modified}'",
                                choice.Index, originalContent, modifiedContent);
                        }
                    }
                }

                // If we modified the content, serialize back to JSON and return as chunk
                if (modified)
                {
                    var modifiedJson = JsonSerializer.Serialize(chunkData, JsonOptions);
                    var modifiedChunk = $"data: {modifiedJson}\n\n";
                    
                    _logger.LogInformation("[CHUNK MANIPULATOR] Citations processed and chunk modified");
                    return modifiedChunk;
                }
            }

            // Return original chunk if no modifications were made
            return chunk;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("[CHUNK MANIPULATOR] JSON parsing error: {Error}", ex.Message);
            return chunk;
        }
        catch (Exception ex)
        {
            _logger.LogError("[CHUNK MANIPULATOR] Unexpected error processing chunk: {Error}", ex.Message);
            return chunk;
        }
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