using Newtonsoft.Json;

namespace OllamaYarpProject;

public class ModelData
{
    public string id { get; set; }
    public string @object { get; set; }
    public long created { get; set; }
    public string owned_by { get; set; }
}

public class SourceRoot
{
    public List<ModelData> data { get; set; }
    public string @object { get; set; }
}

public class OpenWebUiModelData
{
    public string? id { get; set; }
}

public class OpenWebUiSourceRoot
{
    public List<OpenWebUiModelData>? data { get; set; }
}

public class OllamaModelDetails
{
    public string parent_model { get; set; } = "";
    public string format { get; set; } = "gguf";
    public string family { get; set; }
    public List<string> families { get; set; }
    public string parameter_size { get; set; } = "3B";
    public string quantization_level { get; set; } = "Q5_K_M";
}

public class OllamaModel
{
    public string name { get; set; }
    public string model { get; set; }
    public string modified_at { get; set; }
    public long size { get; set; }
    public string digest { get; set; }
    public OllamaModelDetails details { get; set; }
}

public class OllamaRoot
{
    public List<OllamaModel> models { get; set; }
}

public class GemmaModel
{
    //public string License { get; set; }
    //public string Modelfile { get; set; }
    //public string Parameters { get; set; }
    //public string Template { get; set; }
    //public Details Details { get; set; }

    [JsonProperty("model_info")]
    public ModelInfo ModelInfo { get; set; }
    //public List<Tensor> Tensors { get; set; }

    [JsonProperty("capabilities")]
    public List<string> Capabilities { get; set; }
    //public DateTime ModifiedAt { get; set; }
}

public class Details
{
    public string ParentModel { get; set; }
    public string Format { get; set; }
    public string Family { get; set; }
    public List<string> Families { get; set; }
    public string ParameterSize { get; set; }
    public string QuantizationLevel { get; set; }
}

public class ModelInfo
{
    //[JsonProperty("gemma3.attention.head_count")]
    //public int AttentionHeadCount { get; set; }

    //[JsonProperty("gemma3.attention.head_count_kv")]
    //public int AttentionHeadCountKv { get; set; }

    //[JsonProperty("gemma3.attention.key_length")]
    //public int KeyLength { get; set; }

    //[JsonProperty("gemma3.attention.layer_norm_rms_epsilon")]
    //public double LayerNormRmsEpsilon { get; set; }

    //[JsonProperty("gemma3.attention.sliding_window")]
    //public int SlidingWindow { get; set; }

    //[JsonProperty("gemma3.attention.value_length")]
    //public int ValueLength { get; set; }

    //[JsonProperty("gemma3.block_count")]
    //public int BlockCount { get; set; }

    //[JsonProperty("gemma3.context_length")]
    //public int ContextLength { get; set; }

    //[JsonProperty("gemma3.embedding_length")]
    //public int EmbeddingLength { get; set; }

    //[JsonProperty("gemma3.feed_forward_length")]
    //public int FeedForwardLength { get; set; }

    //[JsonProperty("gemma3.final_logit_softcapping")]
    //public int FinalLogitSoftcapping { get; set; }

    //[JsonProperty("gemma3.rope.global.freq_base")]
    //public int RopeGlobalFreqBase { get; set; }

    //[JsonProperty("gemma3.rope.local.freq_base")]
    //public int RopeLocalFreqBase { get; set; }

    [JsonProperty("general.architecture")]
    public string Architecture { get; set; }

    // Other properties can be added as needed, depending on the JSON keys.
    public long ParameterCount { get; set; }
    public int QuantizationVersion { get; set; }

    public Tokenizer Tokenizer { get; set; }
}

public class Tokenizer
{
    [JsonProperty("ggml.add_bos_token")]
    public bool AddBosToken { get; set; }

    [JsonProperty("ggml.add_eos_token")]
    public bool AddEosToken { get; set; }

    [JsonProperty("ggml.add_padding_token")]
    public bool AddPaddingToken { get; set; }

    [JsonProperty("ggml.add_unknown_token")]
    public bool AddUnknownToken { get; set; }

    [JsonProperty("ggml.bos_token_id")]
    public int BosTokenId { get; set; }

    [JsonProperty("ggml.eos_token_id")]
    public int EosTokenId { get; set; }

    [JsonProperty("ggml.padding_token_id")]
    public int PaddingTokenId { get; set; }

    [JsonProperty("ggml.unknown_token_id")]
    public int UnknownTokenId { get; set; }

    // Add other fields as necessary
}

public class Tensor
{
    public string Name { get; set; }
    public string Type { get; set; }
    public List<int> Shape { get; set; }
}