using System;
using Ollama;

namespace OllamaYarpProject.Helpers;

/// <summary>
/// Builder for GenerateChatCompletionResponse.
/// </summary>
public class GenerateChatCompletionResponseBuilder
{
    public Message Message { get; set; }
    public string Model { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Done { get; set; }
    public DoneReason? DoneReason { get; set; }
    public long? TotalDuration { get; set; }
    public long? LoadDuration { get; set; }
    public int? PromptEvalCount { get; set; }
    public long? PromptEvalDuration { get; set; }
    public int? EvalCount { get; set; }
    public long? EvalDuration { get; set; }

    public GenerateChatCompletionResponse Build()
    {
        return new GenerateChatCompletionResponse(
            message: Message,
            model: Model,
            createdAt: CreatedAt,
            done: Done,
            doneReason: DoneReason,
            totalDuration: TotalDuration,
            loadDuration: LoadDuration,
            promptEvalCount: PromptEvalCount,
            promptEvalDuration: PromptEvalDuration,
            evalCount: EvalCount,
            evalDuration: EvalDuration
        );
    }
}
