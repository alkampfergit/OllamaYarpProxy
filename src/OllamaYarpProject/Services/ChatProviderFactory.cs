using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Providers;
using OllamaYarpProject.Configuration;
using Microsoft.Extensions.Options;

namespace OllamaYarpProject.Services;

public class ChatProviderFactory : IChatProviderFactory
{
    private readonly ChatProviderConfiguration _config;

    public ChatProviderFactory(IOptions<ChatProviderConfiguration> config)
    {
        _config = config.Value;
    }

    public IChatProvider CreateProvider()
    {
        var activeProvider = _config.Active;

        return activeProvider switch
        {
            ChatProviders.OpenWebUI => new OpenWebUiProvider(_config),
            ChatProviders.LiteLLM => new LiteLLMProvider(_config),
            _ => throw new InvalidOperationException($"Unknown chat provider: {activeProvider}. Supported providers: {string.Join(", ", ChatProviders.All)}")
        };
    }
}
