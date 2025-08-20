using Microsoft.AspNetCore.Http.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ollama;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Models;
using OllamaYarpProject.Helpers;
using System.Text;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace OllamaYarpProject;

public class StandardTransform : ITransformProvider
{
    private readonly ILogger<StandardTransform> _logger;
    private readonly IChunkManipulatorFactory _chunkManipulatorFactory;
    private readonly IModelRouter _modelRouter;
    private readonly IResponseInterceptorFactory _interceptorFactory;

    public StandardTransform(ILogger<StandardTransform> logger, IChunkManipulatorFactory chunkManipulatorFactory, IModelRouter modelRouter, IResponseInterceptorFactory interceptorFactory)
    {
        _logger = logger;
        _chunkManipulatorFactory = chunkManipulatorFactory;
        _modelRouter = modelRouter;
        _interceptorFactory = interceptorFactory;
    }

    private class RequestResponseData
    {
        public string RequestMethod { get; set; } = "";
        public string RequestPath { get; set; } = "";
        public string RequestBody { get; set; } = "";
        public string ModelName { get; set; } = "";
        public Dictionary<string, string> RequestHeaders { get; set; } = new();
        public DateTime RequestTime { get; set; }
        public string ResponseContent { get; set; } = "";
        public int ResponseStatusCode { get; set; }
        public Dictionary<string, string> ResponseHeaders { get; set; } = new();
    }

    public void Apply(TransformBuilderContext context)
    {
        context.UseDefaultForwarders = true;

        context.AddRequestTransform(async transformContext =>
        {
            var context = transformContext.HttpContext;
            var originalPath = context.Request.Path + context.Request.QueryString;
            var method = context.Request.Method;
            
            _logger.LogDebug("[REQUEST TRANSFORM] Processing {Method} {OriginalPath}", method, originalPath);

            // Capture request data for chat completions
            if (context.Request.Path == "/v1/chat/completions")
            {
                var requestData = new RequestResponseData
                {
                    RequestMethod = method,
                    RequestPath = originalPath,
                    RequestTime = DateTime.UtcNow,
                    RequestHeaders = context.Request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value.AsEnumerable()))
                };

                // Store in HttpContext for later use
                context.Items["RequestResponseData"] = requestData;
            }

            if (context.Request.Path == "/api/tags")
            {
                //we need to change the request to the endpoint models
                transformContext.Path = "/models";
                _logger.LogInformation("[PATH REWRITE] {Method} {OriginalPath} -> {NewPath} (Ollama tags endpoint to models)", 
                    method, originalPath, "/models");
            }
            else if (context.Request.Path == "/v1/chat/completions")
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                string body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                // Capture request body if we have stored request data
                if (context.Items.TryGetValue("RequestResponseData", out var requestDataObj) 
                    && requestDataObj is RequestResponseData requestData)
                {
                    requestData.RequestBody = body;
                }
                 
                var cco = JsonConvert.DeserializeObject<GenerateChatCompletionRequest>(body);
                
                // Store model name in request data for later use by interceptors
                if (context.Items.TryGetValue("RequestResponseData", out var requestDataObj2) 
                    && requestDataObj2 is RequestResponseData requestData2)
                {
                    requestData2.ModelName = cco?.Model ?? "";
                }
                 
                try
                {
                    // Check if the requested model is one of our custom IModel instances
                    var customModel = await _modelRouter.GetCustomModelAsync(cco?.Model);

                    if (customModel != null)
                    {
                        _logger.LogInformation("[CUSTOM MODEL] {Method} {OriginalPath} -> Direct response from custom model '{ModelName}'", 
                            method, originalPath, customModel.Name);
                        
                        var ollamaResponse = cco != null ? await _modelRouter.GenerateDirectResponseAsync(customModel, cco) : null;
                        if (ollamaResponse != null)
                        {
                            var response = transformContext.HttpContext.Response;
                            response.StatusCode = 200;
                            response.ContentType = "application/json";

                            //serialize to json 
                            var jsonResponse = JsonConvert.SerializeObject(ollamaResponse, Formatting.Indented);
                            await response.WriteAsync(jsonResponse);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //ignore --- send the request to the proxy
                }


                transformContext.Path = "/chat/completions";
                _logger.LogInformation("[PATH REWRITE] {Method} {OriginalPath} -> {NewPath} (OpenAI to backend format)", 
                    method, originalPath, "/chat/completions");
            }
            else if (context.Request.Path == "/api/show")
            {
                _logger.LogInformation("[DIRECT RESPONSE] {Method} {OriginalPath} -> Mock model info response", method, originalPath);
                context.Request.EnableBuffering();

                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                string body = await reader.ReadToEndAsync();

                // deserialize in json 
                var json = JsonConvert.DeserializeObject(body) as JObject;
                var model = json?.Value<string>("model");

                // Check if it's one of our custom models
                var customModel = await _modelRouter.GetCustomModelAsync(model);

                var response = transformContext.HttpContext.Response;
                response.StatusCode = 200;
                response.ContentType = "application/json";

                GemmaModel answer = new GemmaModel();
                //answer.License = "MIT";
                //answer.Modelfile = "model.gguf";
                answer.Capabilities = new List<string> { "chat" };
                answer.ModelInfo = new ModelInfo();
                answer.ModelInfo.Architecture = model ?? "unknown";

                // If it's a custom model, add additional info
                if (customModel != null)
                {
                    answer.ModelInfo.Architecture = customModel.Name;
                    // Could add more custom model info here if needed
                }

                var jsonResponse = JsonConvert.SerializeObject(answer, Formatting.Indented);

                await response.WriteAsync(jsonResponse);
            }
            else if (context.Request.Path == "/api/version")
            {
                _logger.LogInformation("[DIRECT RESPONSE] {Method} {OriginalPath} -> Static version response", method, originalPath);
                var response = transformContext.HttpContext.Response;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("{\"version\": \"0.9.6\"}");
            }
            
            // Log final forwarding decision
            var finalPath = transformContext.Path.HasValue ? transformContext.Path.Value : context.Request.Path.ToString();
            _logger.LogInformation("[FORWARDING] {Method} {OriginalPath} -> BACKEND{FinalPath}", 
                method, originalPath, finalPath);
        });

        context.CopyResponseHeaders = true;

        context.AddResponseTransform(async (transformContext) =>
        {
            var context = transformContext.HttpContext;
            var response = transformContext.ProxyResponse;
            var originalPath = context.Request.Path + context.Request.QueryString;
            var method = context.Request.Method;
            
            _logger.LogDebug("[RESPONSE TRANSFORM] Processing response for {Method} {OriginalPath}", method, originalPath);
            
            if (response?.RequestMessage?.RequestUri?.LocalPath == "/models")
            {
                _logger.LogInformation("[RESPONSE TRANSFORM] Transforming /models response from backend to Ollama format");
                //I need to grab the original content and then change the schema
                var content = await response.Content.ReadAsStringAsync();
                var source = JsonConvert.DeserializeObject<SourceRoot>(content);
                
                _logger.LogDebug("[RESPONSE TRANSFORM] Backend returned {ModelCount} models", source?.data?.Count ?? 0);

                // Create models from the proxy response
                var proxyModels = source.data
                    .Select(m => new OllamaModel
                    {
                        name = m.id,
                        model = m.id,
                        modified_at = "2024-02-24T18:29:19.5508829+01:00",
                        size = 1966917458,
                        digest = Guid.NewGuid().ToString(),
                    });

                // Add IModel instances to the list
                var customModels = _modelRouter.GetModels().Select(m => new OllamaModel
                {
                    name = m.Name,
                    model = m.Name,
                    modified_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"),
                    size = 0, // Custom models don't have a size
                    digest = Guid.NewGuid().ToString(),
                });

                var ollamaModels = new OllamaRoot
                {
                    models = proxyModels.Concat(customModels).ToList()
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
            else if (response?.RequestMessage?.RequestUri?.LocalPath == "/chat/completions")
            {
                // Get stored request data to determine the model
                RequestResponseData? requestData = null;
                if (context.Items.TryGetValue("RequestResponseData", out var requestDataObj) 
                    && requestDataObj is RequestResponseData data)
                {
                    requestData = data;
                    requestData.ResponseStatusCode = (int)response.StatusCode;
                    requestData.ResponseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value.AsEnumerable()));
                }
                
                try
                {
                    // Find appropriate interceptor for this model
                    var interceptor = _interceptorFactory.GetInterceptorForModel(requestData?.ModelName ?? "");
                    
                    if (interceptor != null && await interceptor.ShouldInterceptAsync(context, response))
                    {
                        _logger.LogDebug("[RESPONSE INTERCEPT] Using interceptor {InterceptorName} for model {ModelName}", 
                            interceptor.Name, requestData?.ModelName ?? "unknown");
                        
                        await interceptor.InterceptAsync(context, response);
                        transformContext.SuppressResponseBody = true;
                    }
                    else
                    {
                        // Fallback to default behavior for non-streaming responses or when no interceptor is found
                        var content = await response.Content.ReadAsStringAsync();
                        var contentLength = content.Length;
                        
                        if (requestData != null)
                        {
                            requestData.ResponseContent = content;
                        }
                        
                        // Get first few lines for logging
                        var lines = content.Split('\n');
                        var firstLines = string.Join("\n", lines.Take(3));
                        var truncatedContent = firstLines.Length > 200 ? firstLines.Substring(0, 200) + "..." : firstLines;
                        
                        _logger.LogDebug("[RESPONSE INTERCEPT] No interceptor found - using default handling. Total length: {ContentLength} chars, First lines: {FirstContent}", 
                            contentLength, truncatedContent);
                    }
                    
                    // Write request-response data to file
                    if (requestData != null)
                    {
                        await WriteRequestResponseToFile(requestData);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[RESPONSE INTERCEPT] Error intercepting response: {Error}", ex.Message);
                }
            }

            _logger.LogDebug("[RESPONSE COMPLETE] Response sent for {Method} {OriginalPath}", method, originalPath);
        });
    }

    private async Task WriteRequestResponseToFile(RequestResponseData data)
    {
        try
        {
            // Create filename with timestamp
            var timestamp = data.RequestTime.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"request_{timestamp}.txt";
            var executablePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(executablePath, filename);

            // Prepare content
            var content = new StringBuilder();
            content.AppendLine("=== REQUEST ===");
            content.AppendLine($"Time: {data.RequestTime:yyyy-MM-dd HH:mm:ss.fff} UTC");
            content.AppendLine($"Method: {data.RequestMethod}");
            content.AppendLine($"Path: {data.RequestPath}");
            content.AppendLine();
            content.AppendLine("Headers:");
            foreach (var header in data.RequestHeaders)
            {
                content.AppendLine($"  {header.Key}: {header.Value}");
            }
            content.AppendLine();
            content.AppendLine("Body:");
            content.AppendLine(data.RequestBody);
            content.AppendLine();
            content.AppendLine("=== RESPONSE ===");
            content.AppendLine($"Status Code: {data.ResponseStatusCode}");
            content.AppendLine();
            content.AppendLine("Headers:");
            foreach (var header in data.ResponseHeaders)
            {
                content.AppendLine($"  {header.Key}: {header.Value}");
            }
            content.AppendLine();
            content.AppendLine("Content:");
            content.AppendLine(data.ResponseContent);

            // Write to file
            await File.WriteAllTextAsync(filePath, content.ToString());
            
            _logger.LogInformation("[FILE WRITE] Request-response data written to: {FilePath}", filename);
        }
        catch (Exception ex)
        {
            _logger.LogError("[FILE WRITE] Error writing request-response file: {Error}", ex.Message);
        }
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
        _logger.LogInformation("[YARP VALIDATION] Cluster validation called for cluster: {ClusterName}", 
            context.Cluster?.ClusterId ?? "unknown");
    }

    public void ValidateRoute(TransformRouteValidationContext context)
    {
        _logger.LogInformation("[YARP VALIDATION] Route validation called for route: {RouteName}", 
            context.Route?.RouteId ?? "unknown");
    }
}
