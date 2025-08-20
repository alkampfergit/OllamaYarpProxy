using Microsoft.Extensions.Options;
using OllamaYarpProject.Configuration;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject.Services;

public class StreamingResponseProcessorFactory : IStreamingResponseProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly InterceptorConfiguration _configuration;
    private readonly ILogger<StreamingResponseProcessorFactory> _logger;
    private readonly Dictionary<string, IStreamingResponseProcessor> _processors = new();

    public StreamingResponseProcessorFactory(
        IServiceProvider serviceProvider,
        IOptions<InterceptorConfiguration> configuration,
        ILogger<StreamingResponseProcessorFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration.Value;
        _logger = logger;
        InitializeProcessors();
    }

    private void InitializeProcessors()
    {
        var processorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IStreamingResponseProcessor).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var processorType in processorTypes)
        {
            try
            {
                var processor = (IStreamingResponseProcessor)_serviceProvider.GetRequiredService(processorType);
                _processors[processor.Name] = processor;
                _logger.LogInformation("[PROCESSOR FACTORY] Registered processor: {ProcessorName}", processor.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[PROCESSOR FACTORY] Failed to register processor {ProcessorType}: {Error}", 
                    processorType.Name, ex.Message);
            }
        }
    }

    public IStreamingResponseProcessor? GetProcessor(string modelName)
    {
        if (string.IsNullOrEmpty(modelName))
            return null;

        // Check configuration for model-specific processor mapping
        if (_configuration.ModelInterceptorMappings.TryGetValue(modelName, out var processorName))
        {
            if (_processors.TryGetValue(processorName, out var processor))
            {
                _logger.LogDebug("[PROCESSOR FACTORY] Found processor {ProcessorName} for model {ModelName}", 
                    processorName, modelName);
                return processor;
            }
            else
            {
                _logger.LogWarning("[PROCESSOR FACTORY] Configured processor {ProcessorName} not found for model {ModelName}", 
                    processorName, modelName);
            }
        }

        // Fallback: find first processor that wants to handle this model
        foreach (var processor in _processors.Values)
        {
            if (processor.ShouldProcess(null!, modelName)) // HttpContext not needed for model-based decisions
            {
                _logger.LogDebug("[PROCESSOR FACTORY] Found fallback processor {ProcessorName} for model {ModelName}", 
                    processor.Name, modelName);
                return processor;
            }
        }

        return null;
    }
}