using System.Text;
using Newtonsoft.Json;
using OllamaYarpProject.Interfaces;
using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Handlers;

public abstract class BaseModelsResponseHandler : IModelsResponseHandler
{
    protected readonly ILogger _logger;
    protected readonly IModelRouter _modelRouter;

    protected BaseModelsResponseHandler(ILogger logger, IModelRouter modelRouter)
    {
        _logger = logger;
        _modelRouter = modelRouter;
    }

    public async Task HandleModelsResponseAsync(ResponseTransformContext transformContext)
    {
        try
        {
            _logger.LogInformation("[RESPONSE TRANSFORM] Transforming /models response from {ProviderName} backend to Ollama format", GetProviderName());

            // Get backend-specific models
            var backendModels = await GetBackendModelsAsync(transformContext);

            // Add custom models from the model router
            var customModels = _modelRouter.GetModels().Select(m => new OllamaModel
            {
                name = m.Name,
                model = m.Name,
                modified_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"),
                size = 0, // Custom models don't have a size
                digest = Guid.NewGuid().ToString(),
            });

            // Combine all models
            var ollamaModels = new OllamaRoot
            {
                models = backendModels.Concat(customModels).ToList()
            };

            // Serialize and write response
            var ollamaJson = JsonConvert.SerializeObject(ollamaModels, Formatting.Indented);
            await WriteResponseAsync(transformContext, ollamaJson);

            _logger.LogDebug("[RESPONSE TRANSFORM] Successfully transformed {BackendModelCount} backend models + {CustomModelCount} custom models", 
                backendModels.Count(), customModels.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RESPONSE TRANSFORM] Error transforming {ProviderName} models response", GetProviderName());
            transformContext.SuppressResponseBody = false;
            throw;
        }
    }

    protected virtual async Task WriteResponseAsync(ResponseTransformContext transformContext, string jsonContent)
    {
        transformContext.SuppressResponseBody = true;

        // Convert modified JSON to bytes
        var modifiedBytes = Encoding.UTF8.GetBytes(jsonContent);

        // Update the Content-Length header to match the new content
        transformContext.HttpContext.Response.ContentLength = modifiedBytes.Length;

        // Set the correct content type
        transformContext.HttpContext.Response.ContentType = "application/json";

        // Remove compression headers since we're sending uncompressed content
        transformContext.HttpContext.Response.Headers.Remove("Content-Encoding");

        // Write the modified content
        await transformContext.HttpContext.Response.Body.WriteAsync(modifiedBytes);
    }

    protected abstract Task<IEnumerable<OllamaModel>> GetBackendModelsAsync(ResponseTransformContext transformContext);
    protected abstract string GetProviderName();
}
