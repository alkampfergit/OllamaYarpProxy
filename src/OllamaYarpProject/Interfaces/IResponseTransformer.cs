using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OllamaYarpProject.Interfaces;

public interface IResponseTransformer
{
    Task<string> TransformModelsResponseAsync(string content, IEnumerable<IModel> customModels);
    Task<GemmaModel> CreateModelInfoResponseAsync(string? modelName, IEnumerable<IModel> customModels);
}

public class ResponseTransformer : IResponseTransformer
{
    private readonly IJsonSerializer _jsonSerializer;
    private readonly IDateTime _dateTime;
    private readonly ILogger<ResponseTransformer> _logger;

    public ResponseTransformer(IJsonSerializer jsonSerializer, IDateTime dateTime, ILogger<ResponseTransformer> logger)
    {
        _jsonSerializer = jsonSerializer;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task<string> TransformModelsResponseAsync(string content, IEnumerable<IModel> customModels)
    {
        try
        {
            _logger.LogInformation("[RESPONSE TRANSFORM] Transforming /models response from backend to Ollama format");
            
            var source = _jsonSerializer.Deserialize<SourceRoot>(content);
            
            _logger.LogDebug("[RESPONSE TRANSFORM] Backend returned {ModelCount} models", source?.data?.Count ?? 0);

            // Create models from the proxy response
            var proxyModels = source?.data?
                .Select(m => new OllamaModel
                {
                    name = m.id,
                    model = m.id,
                    modified_at = "2024-02-24T18:29:19.5508829+01:00",
                    size = 1966917458,
                    digest = Guid.NewGuid().ToString(),
                }) ?? Enumerable.Empty<OllamaModel>();

            // Add IModel instances to the list
            var customModelsList = customModels.Select(m => new OllamaModel
            {
                name = m.Name,
                model = m.Name,
                modified_at = _dateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"),
                size = 0, // Custom models don't have a size
                digest = Guid.NewGuid().ToString(),
            });

            var ollamaModels = new OllamaRoot
            {
                models = proxyModels.Concat(customModelsList).ToList()
            };
            
            return _jsonSerializer.Serialize(ollamaModels, indented: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RESPONSE TRANSFORM] Error transforming models response");
            return content; // Return original content on error
        }
    }

    public async Task<GemmaModel> CreateModelInfoResponseAsync(string? modelName, IEnumerable<IModel> customModels)
    {
        var customModel = customModels.FirstOrDefault(m => 
            m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));

        var answer = new GemmaModel
        {
            Capabilities = new List<string> { "chat" },
            ModelInfo = new ModelInfo
            {
                Architecture = modelName ?? "unknown"
            }
        };

        // If it's a custom model, add additional info
        if (customModel != null)
        {
            answer.ModelInfo.Architecture = customModel.Name;
            // Could add more custom model info here if needed
        }

        return answer;
    }
}