namespace OllamaYarpProject.Transform;

public interface IStreamingResponseHandler
{
    Task HandleStreamingResponseAsync(HttpContext context, HttpResponseMessage response, RequestResponseData? requestData);
}