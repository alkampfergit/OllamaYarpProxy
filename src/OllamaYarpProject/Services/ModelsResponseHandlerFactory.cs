using OllamaYarpProject.Handlers;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject.Factories;

public class ModelsResponseHandlerFactory : IModelsResponseHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ModelsResponseHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IModelsResponseHandler GetHandler(IChatProvider chatProvider)
    {
        return chatProvider.Name switch
        {
            ChatProviders.OpenWebUI => _serviceProvider.GetRequiredService<OpenWebUIModelsResponseHandler>(),
            ChatProviders.LiteLLM => _serviceProvider.GetRequiredService<LiteLLMModelsResponseHandler>(),
            _ => throw new InvalidOperationException($"No models response handler found for provider: {chatProvider.Name}")
        };
    }
}
