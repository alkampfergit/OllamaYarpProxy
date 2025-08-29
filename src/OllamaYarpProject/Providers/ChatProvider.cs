using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Configuration;

namespace OllamaYarpProject;

public abstract class ChatProvider : IChatProvider
{
    public abstract string Name { get; }
    public abstract string ModelsEndpoint { get; }
    public abstract string ChatCompletionsEndpoint { get; }
    public AuthenticationConfiguration Authentication { get; }

    protected ChatProvider(ChatProviderConfiguration providerConfig)
    {
        Authentication = providerConfig.Authentication;
    }
}
