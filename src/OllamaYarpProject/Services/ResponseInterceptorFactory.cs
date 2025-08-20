using Microsoft.Extensions.Options;
using OllamaYarpProject.Configuration;
using OllamaYarpProject.Interfaces;

namespace OllamaYarpProject.Services;

public class ResponseInterceptorFactory : IResponseInterceptorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly InterceptorConfiguration _configuration;
    private readonly ILogger<ResponseInterceptorFactory> _logger;
    private readonly Dictionary<string, IResponseInterceptor> _interceptors = new();

    public ResponseInterceptorFactory(
        IServiceProvider serviceProvider,
        IOptions<InterceptorConfiguration> configuration,
        ILogger<ResponseInterceptorFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration.Value;
        _logger = logger;
        InitializeInterceptors();
    }

    private void InitializeInterceptors()
    {
        var interceptorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IResponseInterceptor).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var interceptorType in interceptorTypes)
        {
            try
            {
                var interceptor = (IResponseInterceptor)_serviceProvider.GetRequiredService(interceptorType);
                _interceptors[interceptor.Name] = interceptor;
                _logger.LogInformation("[INTERCEPTOR FACTORY] Registered interceptor: {InterceptorName}", interceptor.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[INTERCEPTOR FACTORY] Failed to register interceptor {InterceptorType}: {Error}", 
                    interceptorType.Name, ex.Message);
            }
        }
    }

    public IResponseInterceptor? GetInterceptor(string name)
    {
        _interceptors.TryGetValue(name, out var interceptor);
        return interceptor;
    }

    public IResponseInterceptor? GetInterceptorForModel(string modelName)
    {
        if (string.IsNullOrEmpty(modelName))
            return null;

        if (_configuration.ModelInterceptorMappings.TryGetValue(modelName, out var interceptorName))
        {
            var interceptor = GetInterceptor(interceptorName);
            if (interceptor != null)
            {
                _logger.LogDebug("[INTERCEPTOR FACTORY] Found interceptor {InterceptorName} for model {ModelName}", 
                    interceptorName, modelName);
                return interceptor;
            }
            else
            {
                _logger.LogWarning("[INTERCEPTOR FACTORY] Configured interceptor {InterceptorName} not found for model {ModelName}", 
                    interceptorName, modelName);
            }
        }

        return null;
    }
}