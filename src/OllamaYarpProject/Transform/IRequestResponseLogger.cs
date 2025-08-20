namespace OllamaYarpProject.Transform;

public interface IRequestResponseLogger
{
    Task WriteRequestResponseToFileAsync(RequestResponseData data);
}