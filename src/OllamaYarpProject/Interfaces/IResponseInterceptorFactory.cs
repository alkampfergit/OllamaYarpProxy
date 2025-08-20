namespace OllamaYarpProject.Interfaces;

public interface IResponseInterceptorFactory
{
    IResponseInterceptor? GetInterceptor(string name);
    IResponseInterceptor? GetInterceptorForModel(string modelName);
}