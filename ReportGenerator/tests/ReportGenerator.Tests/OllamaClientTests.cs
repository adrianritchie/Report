using System.Net;
using System.Text;
using System.Text.Json;
using ReportGenerator.Configuration;
using ReportGenerator.Ollama;

namespace ReportGenerator.Tests;

/// <summary>
/// Unit tests for <see cref="OllamaClient"/> using a stubbed <see cref="HttpMessageHandler"/>.
/// No real network calls are made.
/// </summary>
public sealed class OllamaClientTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static OllamaOptions DefaultOptions => new()
    {
        BaseUrl = "http://localhost:11434/",
        Model = "llama3.2",
        TimeoutSeconds = 30
    };

    /// <summary>
    /// Builds an <see cref="HttpClient"/> whose base address is set and whose
    /// handler always returns the supplied <paramref name="response"/>.
    /// </summary>
    private static HttpClient BuildHttpClient(HttpResponseMessage response)
    {
        var handler = new StubHttpMessageHandler(response);
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };
        return client;
    }

    private static HttpResponseMessage OkResponse(string content, string model = "llama3.2")
    {
        var body = new
        {
            model,
            message = new { role = "assistant", content },
            done = true,
            done_reason = "stop"
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendPromptAsync_ReturnsMessageContent_OnSuccess()
    {
        // Arrange
        var expected = "Great job, student!";
        var client = new OllamaClient(BuildHttpClient(OkResponse(expected)), DefaultOptions);

        // Act
        var result = await client.SendPromptAsync("Assess this student.");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SendPromptAsync_UsesModelFromOptions()
    {
        // Arrange
        string? capturedBody = null;
        var handler = new CapturingHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return OkResponse("ok", "mistral");
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var options = new OllamaOptions { BaseUrl = "http://localhost:11434/", Model = "mistral", TimeoutSeconds = 30 };
        var client = new OllamaClient(http, options);

        // Act
        await client.SendPromptAsync("test prompt");

        // Assert
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.Equal("mistral", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task SendPromptAsync_SendsStreamFalse()
    {
        // Arrange
        string? capturedBody = null;
        var handler = new CapturingHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return OkResponse("ok");
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var client = new OllamaClient(http, DefaultOptions);

        // Act
        await client.SendPromptAsync("test prompt");

        // Assert
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.False(doc.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task SendPromptAsync_SendsPromptAsUserMessage()
    {
        // Arrange
        string? capturedBody = null;
        var handler = new CapturingHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return OkResponse("ok");
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434/") };
        var client = new OllamaClient(http, DefaultOptions);

        const string prompt = "My specific prompt text";

        // Act
        await client.SendPromptAsync(prompt);

        // Assert
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        var msg = messages[0];
        Assert.Equal("user", msg.GetProperty("role").GetString());
        Assert.Equal(prompt, msg.GetProperty("content").GetString());
    }

    [Fact]
    public async Task SendPromptAsync_Throws_WhenHttpResponseIsNotSuccess()
    {
        // Arrange
        var errorResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error")
        };
        var client = new OllamaClient(BuildHttpClient(errorResponse), DefaultOptions);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendPromptAsync("prompt"));
    }

    [Fact]
    public async Task SendPromptAsync_Throws_WhenResponseBodyIsNull()
    {
        // Arrange — return valid HTTP 200 but with an empty JSON null body
        var nullBody = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };
        var client = new OllamaClient(BuildHttpClient(nullBody), DefaultOptions);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendPromptAsync("prompt"));
    }

    [Fact]
    public async Task SendPromptAsync_Throws_WhenMessageContentIsEmpty()
    {
        // Arrange — message content is whitespace
        var body = new
        {
            model = "llama3.2",
            message = new { role = "assistant", content = "   " },
            done = true,
            done_reason = "stop"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        var client = new OllamaClient(BuildHttpClient(response), DefaultOptions);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendPromptAsync("prompt"));
    }

    [Fact]
    public async Task SendPromptAsync_Throws_WhenMessageIsNull()
    {
        // Arrange — response has no "message" field (null)
        var body = new
        {
            model = "llama3.2",
            message = (object?)null,
            done = true,
            done_reason = "stop"
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        var client = new OllamaClient(BuildHttpClient(response), DefaultOptions);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendPromptAsync("prompt"));
    }

    // ── ListModelsAsync tests ─────────────────────────────────────────────────

    [Fact]
    public async Task ListModelsAsync_ReturnsModelNames_OnSuccess()
    {
        // Arrange
        var body = new
        {
            models = new[]
            {
                new { name = "llama3.2" },
                new { name = "mistral" },
                new { name = "codellama" }
            }
        };
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        var client = new OllamaClient(BuildHttpClient(response), DefaultOptions);

        // Act
        var models = await client.ListModelsAsync();

        // Assert — names returned and sorted alphabetically
        Assert.Equal(3, models.Count);
        Assert.Equal("codellama", models[0]);
        Assert.Equal("llama3.2", models[1]);
        Assert.Equal("mistral", models[2]);
    }

    [Fact]
    public async Task ListModelsAsync_ThrowsHttpRequestException_WhenUnreachable()
    {
        // Arrange — simulate a non-success HTTP status
        var errorResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("Service Unavailable")
        };
        var client = new OllamaClient(BuildHttpClient(errorResponse), DefaultOptions);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ListModelsAsync());
    }

    // ── stub handlers ─────────────────────────────────────────────────────────

    private sealed class StubHttpMessageHandler(HttpResponseMessage response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class CapturingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
