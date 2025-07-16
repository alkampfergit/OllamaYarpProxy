using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace OllamaYarpProject;
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

public class O3ProClient
{
    private readonly IOptionsMonitor<O3ProConfig> _config;
    private readonly ILogger<O3ProClient> _logger;

    public O3ProClient(IOptionsMonitor<O3ProConfig> config, ILogger<O3ProClient> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> CreateResponseAsync(string prompt)
    {
        var currentConfig = _config.CurrentValue;

        if (string.IsNullOrEmpty(currentConfig.Endpoint) || string.IsNullOrEmpty(currentConfig.ApiKey))
        {
            _logger.LogError("O3Pro configuration is incomplete. Endpoint: {hasEndpoint}, ApiKey: {hasApiKey}",
                !string.IsNullOrEmpty(currentConfig.Endpoint), !string.IsNullOrEmpty(currentConfig.ApiKey));
            throw new InvalidOperationException("O3Pro configuration is incomplete");
        }

        try
        {
            var options = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2025_04_01_Preview);

            var azureClient = new AzureOpenAIClient(
                new Uri(currentConfig.Endpoint),
                new AzureKeyCredential(currentConfig.ApiKey),
                options);

            var responseClient = azureClient.GetOpenAIResponseClient(currentConfig.DeploymentName);

            var response = await responseClient.CreateResponseAsync(prompt);

            _logger.LogInformation("Successfully called O3Pro with deployment: {deployment}", currentConfig.DeploymentName);

            return ProcessResponse(response.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling O3Pro API with deployment: {deployment}", currentConfig.DeploymentName);
            throw;
        }
    }

    private string ProcessResponse(OpenAIResponse response)
    {
        var result = new List<string>();

        foreach (var item in response.OutputItems)
        {
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            if (item is ReasoningResponseItem rri)
            {
                if (rri.SummaryParts.Count > 0)
                {
                    result.Add("REASONING STEPS:");
                    foreach (var sp in rri.SummaryParts)
                    {
                        result.Add($"ReasonStep: {sp}");
                    }
                    result.Add("");
                }
            }
            else if (item is MessageResponseItem mri)
            {
                result.Add("ANSWER:");
                foreach (var content in mri.Content)
                {
                    result.Add($"Content: {content.Text}");
                }
            }
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        return string.Join(Environment.NewLine, result);
    }
}
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.