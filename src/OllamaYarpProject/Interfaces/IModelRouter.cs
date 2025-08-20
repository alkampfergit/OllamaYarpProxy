using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ollama;
using OllamaYarpProject.Helpers;

namespace OllamaYarpProject.Interfaces;

public interface IModelRouter
{
    Task<IModel?> GetCustomModelAsync(string? modelName);
    Task<GenerateChatCompletionResponse?> GenerateDirectResponseAsync(IModel model, GenerateChatCompletionRequest request);
}

public class ModelRouter : IModelRouter
{
    private readonly IEnumerable<IModel> _models;
    private readonly ILogger<ModelRouter> _logger;
    private readonly IDateTime _dateTime;

    public ModelRouter(IEnumerable<IModel> models, ILogger<ModelRouter> logger, IDateTime dateTime)
    {
        _models = models;
        _logger = logger;
        _dateTime = dateTime;
    }

    public Task<IModel?> GetCustomModelAsync(string? modelName)
    {
        if (string.IsNullOrEmpty(modelName))
            return Task.FromResult<IModel?>(null);

        var customModel = _models.FirstOrDefault(m => 
            m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(customModel);
    }

    public async Task<GenerateChatCompletionResponse?> GenerateDirectResponseAsync(IModel model, GenerateChatCompletionRequest request)
    {
        try
        {
            _logger.LogInformation("[CUSTOM MODEL] Direct response from custom model '{ModelName}'", model.Name);
            
            // Create a single message from the chat completion messages
            var stringBuilder = new StringBuilder();
            if (request.Messages != null)
            {
                foreach (var message in request.Messages)
                {
                    stringBuilder.AppendLine($"Role: {message.Role}");
                    stringBuilder.AppendLine(message.Content);
                    stringBuilder.AppendLine("-------");
                }
            }

            var modelResponse = await model.GenerateAsync(stringBuilder.ToString());

            return new GenerateChatCompletionResponseBuilder
            {
                Message = new Message
                {
                    Role = MessageRole.Assistant,
                    Content = modelResponse
                },
                Model = model.Name,
                CreatedAt = _dateTime.UtcNow,
                Done = true,
                DoneReason = DoneReasonEnum.Stop,
            }.Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CUSTOM MODEL] Error generating response from model '{ModelName}'", model.Name);
            return null;
        }
    }
}