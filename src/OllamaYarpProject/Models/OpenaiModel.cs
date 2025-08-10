using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel; // added for ApiKeyCredential

namespace OllamaYarpProject.Interfaces;

public class OpenaiModel : IModel
{
    private readonly OpenAIClient _client;
    private readonly ChatClient _chatClient;
    private readonly string _modelId;
    public string Name { get; }
    public string Description { get; }

    public OpenaiModel(IConfiguration configuration, string modelId, string name, string description)
    {
        _modelId = modelId;
        Name = name;
        Description = description;
        var apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
        var endpoint = configuration["OpenAI:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            _client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
        }
        else
        {
            _client = new OpenAIClient(new ApiKeyCredential(apiKey));
        }
        _chatClient = _client.GetChatClient(_modelId);
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt cannot be empty", nameof(prompt));

        var messages = new List<ChatMessage> { new UserChatMessage(prompt) };


        var result = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = 0.5f }, cancellationToken).ConfigureAwait(false);
        var completion = result.Value; // extract ChatCompletion from ClientResult wrapper
        if (completion?.Content is null || completion.Content.Count == 0) return string.Empty;
        var text = string.Concat(completion.Content.Select(part => part.Text));
        return text.Trim();
    }
}
