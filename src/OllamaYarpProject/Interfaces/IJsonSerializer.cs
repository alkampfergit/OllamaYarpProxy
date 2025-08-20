using Newtonsoft.Json;

namespace OllamaYarpProject.Interfaces;

public interface IJsonSerializer
{
    string Serialize<T>(T obj, bool indented = false);
    T? Deserialize<T>(string json);
    object? Deserialize(string json);
}

public class NewtonsoftJsonSerializer : IJsonSerializer
{
    public string Serialize<T>(T obj, bool indented = false)
    {
        var formatting = indented ? Formatting.Indented : Formatting.None;
        return JsonConvert.SerializeObject(obj, formatting);
    }

    public T? Deserialize<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }

    public object? Deserialize(string json)
    {
        return JsonConvert.DeserializeObject(json);
    }
}