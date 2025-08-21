using Yarp.ReverseProxy.Transforms;

namespace OllamaYarpProject.Interfaces;

public interface IModelsResponseHandler
{
    Task HandleModelsResponseAsync(ResponseTransformContext transformContext);
}
