using System.Text;
using Newtonsoft.Json;
using OllamaYarpProject.Interfaces;
using Yarp.ReverseProxy.Transforms;
using ZstdNet;

namespace OllamaYarpProject.Handlers;

public class OpenWebUIModelsResponseHandler : BaseModelsResponseHandler
{
    public OpenWebUIModelsResponseHandler(
        ILogger<OpenWebUIModelsResponseHandler> logger,
        IModelRouter modelRouter) : base(logger, modelRouter)
    {
    }

    protected override async Task<IEnumerable<OllamaModel>> GetBackendModelsAsync(ResponseTransformContext transformContext)
    {
        var response = transformContext.ProxyResponse;

        // Read and decompress content if needed
        var contentBytes = await response!.Content.ReadAsByteArrayAsync();
        var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();
        
        string content;
        if (contentEncoding?.Equals("zstd", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogDebug("[RESPONSE TRANSFORM] Decompressing zstd content ({ContentBytes} bytes)", contentBytes.Length);
            using var decompressor = new Decompressor();
            var decompressedData = decompressor.Unwrap(contentBytes);
            content = Encoding.UTF8.GetString(decompressedData);
        }
        else
        {
            content = Encoding.UTF8.GetString(contentBytes);
        }

        var source = JsonConvert.DeserializeObject<OpenWebUiSourceRoot>(content);
        _logger.LogDebug("[RESPONSE TRANSFORM] OpenWebUI backend returned {ModelCount} models", source?.data?.Count ?? 0);

        // Create models from the OpenWebUI response
        var proxyModels = source!.data?
            .Select(m => new OllamaModel
            {
                name = m.id ?? "unknown",
                model = m.id ?? "unknown",
                modified_at = "2024-02-24T18:29:19.5508829+01:00",
                size = 1966917458,
                digest = Guid.NewGuid().ToString(),
            }) ?? Enumerable.Empty<OllamaModel>();

        return proxyModels;
    }

    protected override string GetProviderName() => ChatProviders.OpenWebUI;
}
