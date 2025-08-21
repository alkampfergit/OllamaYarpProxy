namespace OllamaYarpProject.Interfaces;

public interface IModelsResponseHandlerFactory
{
    IModelsResponseHandler GetHandler(IChatProvider chatProvider);
}
