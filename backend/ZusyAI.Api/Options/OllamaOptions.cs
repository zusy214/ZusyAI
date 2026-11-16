namespace ZusyAI.Api.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string LocalBaseUrl { get; set; } = "http://127.0.0.1:11434/api";

    public string LocalModel { get; set; } = "qwen3:1.7b";

    public string LocalVisionModel { get; set; } = "qwen3-vl:2b";

    public string CloudBaseUrl { get; set; } = "https://ollama.com/api";

    public string CloudModel { get; set; } = "gpt-oss:20b";

    public string CloudVisionModel { get; set; } = "qwen3-vl:235b-cloud";
}
