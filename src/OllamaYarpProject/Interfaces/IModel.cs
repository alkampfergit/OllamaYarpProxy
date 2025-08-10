using System;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaYarpProject.Interfaces;

public interface IModel
{
    string Name { get; }
    string Description { get; }
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
