namespace ReportGenerator.Ollama;

public interface IOllamaClient
{
    /// <summary>
    /// Sends a prompt to the Ollama Chat API and returns the model's reply text.
    /// </summary>
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sorted list of model names available on the Ollama server.
    /// </summary>
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default);
}
