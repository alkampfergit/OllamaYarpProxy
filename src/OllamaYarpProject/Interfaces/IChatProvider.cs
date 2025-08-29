using OllamaYarpProject.Configuration;

namespace OllamaYarpProject.Interfaces;

public interface IChatProvider
{
    string Name { get; }
    string ModelsEndpoint { get; }
    string ChatCompletionsEndpoint { get; }
    AuthenticationConfiguration Authentication { get; }
    
    bool Is(string provider) => Name.Equals(provider, StringComparison.OrdinalIgnoreCase);
}
