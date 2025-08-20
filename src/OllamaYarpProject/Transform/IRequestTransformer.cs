using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public interface IRequestTransformer
{
    Task<bool> TransformRequestAsync(RequestTransformContext context);
}