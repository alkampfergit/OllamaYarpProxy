namespace OllamaYarpProject.Interfaces;

public interface IResponseInterceptor
{
    string Name { get; }
    Task<bool> ShouldInterceptAsync(HttpContext context, HttpResponseMessage response);
    Task InterceptAsync(HttpContext context, HttpResponseMessage response);
}