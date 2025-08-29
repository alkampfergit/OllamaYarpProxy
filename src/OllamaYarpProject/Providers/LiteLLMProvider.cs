using OllamaYarpProject.Configuration;

namespace OllamaYarpProject.Providers;

public class LiteLLMProvider : ChatProvider
{
    public override string Name => ChatProviders.LiteLLM;
    public override string ModelsEndpoint => "/models";
    public override string ChatCompletionsEndpoint => "/chat/completions";

    public LiteLLMProvider(ChatProviderConfiguration providerConfig) : base(providerConfig)
    {
    }
}
