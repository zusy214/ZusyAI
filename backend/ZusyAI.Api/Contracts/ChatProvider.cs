namespace ZusyAI.Api.Contracts;

/// <summary>Selects where a chat request is processed.</summary>
public enum ChatProvider
{
    /// <summary>Use the Ollama service on this computer.</summary>
    Local,

    /// <summary>Use Ollama's hosted cloud API.</summary>
    Cloud
}
