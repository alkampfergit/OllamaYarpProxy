namespace OllamaYarpProject.Configuration;

public class ChatProviderConfiguration
{
    public string Active { get; set; } = ChatProviders.LiteLLM;
    public AuthenticationConfiguration Authentication { get; set; } = new();
}
