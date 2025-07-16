using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace OllamaYarpProject;

public class StandardTransform : ITransformProvider
{
    private readonly ILogger<StandardTransform> _logger;
    private readonly IOptionsMonitor<O3ProConfig> _o3ProConfig;

    public StandardTransform(ILogger<StandardTransform> logger, IOptionsMonitor<O3ProConfig> o3ProConfig)
    {
        _logger = logger;
        _o3ProConfig = o3ProConfig;
    }

    public void Apply(TransformBuilderContext context)
    {
        context.UseDefaultForwarders = true;
        var destinationsUrl = context
            .Cluster
            .Destinations
            .Values
            .Select(v => v.Address) //#16250
            .ToHashSet();

        context.AddRequestTransform(async transformContext =>
        {
            var context = transformContext.HttpContext;

            if (context.Request.Path == "/api/tags")
            {
                //we need to change the request to the endpoint models
                transformContext.Path = "/models";
                _logger.LogInformation("Proxy: Request path rewritten from /api/tags to /api/models");
            }
            else if (context.Request.Path == "/v1/chat/completions")
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                string body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                try
                {
                    var json = JsonConvert.DeserializeObject(body) as JObject;
                    var model = json?.Value<string>("model");
                    
                    if (model == "o3-pro")
                    {
                        var currentConfig = _o3ProConfig.CurrentValue;
                        _logger.LogError("Proxy: Request to /v1/chat/completions with model 'o3-pro' detected. Endpoint: {endpoint}, ApiKey configured: {hasApiKey}", 
                            currentConfig.Endpoint, !string.IsNullOrEmpty(currentConfig.ApiKey));
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning($"Proxy: Failed to parse request body as JSON: {ex.Message}");
                }

                transformContext.Path = "/chat/completions";
                _logger.LogInformation("Proxy: Request path rewritten from v1/chat/completions to chat/completions");
            }
            else if (context.Request.Path == "/api/show")
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                string body = await reader.ReadToEndAsync();

                // deserialize in json 
                var json = JsonConvert.DeserializeObject(body) as JObject;
                var model = json.Value<string>("model");

                var response = transformContext.HttpContext.Response;
                response.StatusCode = 200;
                response.ContentType = "application/json";

                GemmaModel answer = new GemmaModel();
                //answer.License = "MIT";
                //answer.Modelfile = "model.gguf";
                answer.Capabilities = new List<string> { "chat" };
                answer.ModelInfo = new ModelInfo();
                answer.ModelInfo.Architecture = model;

                var jsonResponse = JsonConvert.SerializeObject(answer, Formatting.Indented);

                await response.WriteAsync(jsonResponse);
            }
            else if (context.Request.Path == "/api/version") 
            {
                var response = transformContext.HttpContext.Response;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("{\"version\": \"0.9.6\"}");
            }
            _logger.LogDebug($"Proxy: Request {context.Request.GetDisplayUrl()} method {context.Request.Method} proxied to {transformContext.Path}");
        });

        context.CopyResponseHeaders = true;

        context.AddResponseTransform(async (transformContext) =>
        {
            var context = transformContext.HttpContext;
            var response = transformContext.ProxyResponse;
            if (response.RequestMessage.RequestUri.LocalPath == "/models")
            {
                //I need to grab the original content and then change the schema
                var content = await response.Content.ReadAsStringAsync();
                var source = JsonConvert.DeserializeObject<SourceRoot>(content);

                var ollamaModels = new OllamaRoot
                {
                    models = source.data
                        .Select(m => new OllamaModel
                        {
                            name = m.id,
                            model = m.id,
                            modified_at = "2024-02-24T18:29:19.5508829+01:00",
                            size = 1966917458,
                            digest = Guid.NewGuid().ToString(),
                        })
                        .ToList()
                };
                var ollamaJson = JsonConvert.SerializeObject(ollamaModels, Formatting.Indented);

                transformContext.SuppressResponseBody = true;

                // Convert modified JSON to bytes
                var modifiedBytes = System.Text.Encoding.UTF8.GetBytes(ollamaJson);

                // Update the Content-Length header to match the new content
                transformContext.HttpContext.Response.ContentLength = modifiedBytes.Length;

                // Set the correct content type
                transformContext.HttpContext.Response.ContentType = "application/json";

                // Write the modified content
                await transformContext.HttpContext.Response.Body.WriteAsync(modifiedBytes);
            }

            _logger.LogDebug("Proxy: Request {0} proxied", context.Request.GetDisplayUrl());
        });
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
        _logger.LogInformation("StandardTransform.ValidateCluster called");
    }

    public void ValidateRoute(TransformRouteValidationContext context)
    {
        _logger.LogInformation("StandardTransform.ValidateRoute called");
    }
}
