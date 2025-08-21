using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace OllamaYarpProject;

public class StandardTransform : ITransformProvider
{
    private readonly ILogger<StandardTransform> _logger;
    private readonly Transform.IPathTransformer _pathTransformer;
    private readonly Transform.IRequestTransformer _requestTransformer;
    private readonly Transform.IResponseTransformer _responseTransformer;


    public StandardTransform(
        ILogger<StandardTransform> logger,
        Transform.IPathTransformer pathTransformer,
        Transform.IRequestTransformer requestTransformer,
        Transform.IResponseTransformer responseTransformer)
    {
        _logger = logger;
        _pathTransformer = pathTransformer;
        _requestTransformer = requestTransformer;
        _responseTransformer = responseTransformer;
    }

    public void Apply(TransformBuilderContext context)
    {
        context.UseDefaultForwarders = true;

        context.AddRequestTransform(async transformContext =>
        {
            var originalPath = transformContext.HttpContext.Request.Path + transformContext.HttpContext.Request.QueryString;
            var method = transformContext.HttpContext.Request.Method;

            _logger.LogDebug("[REQUEST TRANSFORM] Processing {Method} {OriginalPath}", method, originalPath);


            // Apply path transformations first
            var pathTransformed = _pathTransformer.TransformPath(transformContext);

            // Apply request transformations (handles complex logic and direct responses)
            var requestHandled = await _requestTransformer.TransformRequestAsync(transformContext);

            if (!requestHandled)
            {
                // Log final forwarding decision if not handled directly
                var finalPath = transformContext.Path.HasValue ? transformContext.Path.Value : transformContext.HttpContext.Request.Path.ToString();
                _logger.LogInformation("[FORWARDING] {Method} {OriginalPath} -> BACKEND{FinalPath}",
                    method, originalPath, finalPath);
            }
        });

        context.CopyResponseHeaders = true;

        context.AddResponseTransform(async (transformContext) =>
        {
            await _responseTransformer.TransformResponseAsync(transformContext);
        });
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
