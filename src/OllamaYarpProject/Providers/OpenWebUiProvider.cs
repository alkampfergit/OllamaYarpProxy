using OllamaYarpProject.Configuration;

namespace OllamaYarpProject.Providers;

public class OpenWebUiProvider : ChatProvider
{
    public override string Name => ChatProviders.OpenWebUI;
    public override string ModelsEndpoint => "/api/v1/models";
    public override string ChatCompletionsEndpoint => "/api/v1/chat/completions";

    public OpenWebUiProvider(ChatProviderConfiguration providerConfig) : base(providerConfig)
    {
    }
}
