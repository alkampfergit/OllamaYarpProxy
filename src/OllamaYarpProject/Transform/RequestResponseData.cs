namespace OllamaYarpProject.Transform;

public class RequestResponseData
{
    public string RequestMethod { get; set; } = "";
    public string RequestPath { get; set; } = "";
    public string RequestBody { get; set; } = "";
    public string ModelName { get; set; } = "";
    public Dictionary<string, string> RequestHeaders { get; set; } = new();
    public DateTime RequestTime { get; set; }
    public string ResponseContent { get; set; } = "";
    public int ResponseStatusCode { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
}