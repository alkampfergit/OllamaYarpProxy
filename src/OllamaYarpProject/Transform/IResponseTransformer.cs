using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public interface IResponseTransformer
{
    Task TransformResponseAsync(ResponseTransformContext context);
}