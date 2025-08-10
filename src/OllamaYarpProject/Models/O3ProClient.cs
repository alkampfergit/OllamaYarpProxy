using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject.Models;

public class O3ProClient : IModel
{
    private static readonly HttpClient _httpClient = new();
    private readonly IOptionsMonitor<O3ProConfig> _config;
    private readonly ILogger<O3ProClient> _logger;

    public string Name { get; }
    public string Description { get; }

    public O3ProClient(IOptionsMonitor<O3ProConfig> config, ILogger<O3ProClient> logger)
    {
        _config = config;
        _logger = logger;
        var current = _config.CurrentValue;
        Name = string.IsNullOrWhiteSpace(current.DeploymentName) ? "o3-pro" : current.DeploymentName;
        Description = "Azure/OpenAI O3 Pro reasoning model client";
    }

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default) => CreateResponseAsync(prompt, cancellationToken);

    public async Task<string> CreateResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var currentConfig = _config.CurrentValue;
        if (string.IsNullOrEmpty(currentConfig.Endpoint) || string.IsNullOrEmpty(currentConfig.ApiKey))
        {
            _logger.LogError("O3Pro configuration is incomplete. Endpoint: {hasEndpoint}, ApiKey: {hasApiKey}",
                !string.IsNullOrEmpty(currentConfig.Endpoint), !string.IsNullOrEmpty(currentConfig.ApiKey));
            throw new InvalidOperationException("O3Pro configuration is incomplete");
        }
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        try
        {
            var endpoint = currentConfig.Endpoint.TrimEnd('/');
            const string apiVersion = "2024-02-15-preview"; // fixed: removed reference to missing ApiVersion property
            string url;
            bool isAzure = endpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("ai.azure.com", StringComparison.OrdinalIgnoreCase);
            if (isAzure)
            {
                url = $"{endpoint}/openai/deployments/{currentConfig.DeploymentName}/chat/completions?api-version={apiVersion}";
            }
            else
            {
                url = "https://api.openai.com/v1/chat/completions";
            }

            var payload = new
            {
                model = isAzure ? null : currentConfig.DeploymentName,
                messages = new object[] { new { role = "user", content = prompt } },
                temperature = 0.2f,
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (isAzure)
            {
                req.Headers.TryAddWithoutValidation("api-key", currentConfig.ApiKey);
            }
            else
            {
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", currentConfig.ApiKey);
            }

            using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("O3Pro request failed: {Status} - {Body}", resp.StatusCode, err);
                throw new InvalidOperationException($"O3Pro request failed: {(int)resp.StatusCode} {resp.ReasonPhrase} - {err}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
                {
                    return contentEl.GetString()!.Trim();
                }
                if (first.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    return textEl.GetString()!.Trim();
                }
            }
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling O3Pro API with deployment: {deployment}", currentConfig.DeploymentName);
            throw;
        }
    }
}