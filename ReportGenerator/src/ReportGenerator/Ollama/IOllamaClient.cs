namespace ReportGenerator.Ollama;

public interface IOllamaClient
{
    /// <summary>
    /// Sends a prompt to the Ollama Chat API and returns the model's reply text.
    /// </summary>
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);
}
