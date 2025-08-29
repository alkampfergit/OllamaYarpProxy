using Newtonsoft.Json;
using OllamaYarpProject.Interfaces;
using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Handlers;

public class LiteLLMModelsResponseHandler : BaseModelsResponseHandler
{
    public LiteLLMModelsResponseHandler(
        ILogger<LiteLLMModelsResponseHandler> logger,
        IModelRouter modelRouter) : base(logger, modelRouter)
    {
    }

    protected override async Task<IEnumerable<OllamaModel>> GetBackendModelsAsync(ResponseTransformContext transformContext)
    {
        var response = transformContext.ProxyResponse;
        
        // Grab the original content and change the schema
        var content = await response!.Content.ReadAsStringAsync();
        var source = JsonConvert.DeserializeObject<SourceRoot>(content);
        
        _logger.LogDebug("[RESPONSE TRANSFORM] LiteLLM backend returned {ModelCount} models", source?.data?.Count ?? 0);

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

        return proxyModels;
    }

    protected override string GetProviderName() => ChatProviders.LiteLLM;
}
