using System.Net.Http.Json;
using System.Text.Json;
using ReportGenerator.Configuration;

namespace ReportGenerator.Ollama;

public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaClient(HttpClient http, OllamaOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest
        {
            Model = _options.Model,
            Stream = false,
            Messages =
            [
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = prompt
                }
            ]
        };

        var response = await _http.PostAsJsonAsync("api/chat", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned an empty response body.");

        if (body.Message is null || string.IsNullOrWhiteSpace(body.Message.Content))
            throw new InvalidOperationException(
                $"Ollama returned a response with no message content. done_reason={body.DoneReason}");

        return body.Message.Content;
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("api/tags", cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Ollama returned an empty response body for /api/tags.");

        return body.Models
            .Select(m => m.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n)
            .ToList()
            .AsReadOnly();
    }
}
