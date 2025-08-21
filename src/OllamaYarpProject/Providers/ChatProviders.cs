namespace OllamaYarpProject;

/// <summary>
/// Central list of supported chat providers to avoid magic strings scattered across the codebase.
/// Extend this class when adding new providers.
/// </summary>
public static class ChatProviders
{
    public const string LiteLLM = "LiteLLM";
    public const string OpenWebUI = "OpenWebUI";

    public static readonly IReadOnlyCollection<string> All = new[] { LiteLLM, OpenWebUI };

    public static bool IsKnown(string? provider) => provider != null && All.Contains(provider);
}
