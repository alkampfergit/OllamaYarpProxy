using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Transform;

public interface IPathTransformer
{
    bool TransformPath(RequestTransformContext context);
}