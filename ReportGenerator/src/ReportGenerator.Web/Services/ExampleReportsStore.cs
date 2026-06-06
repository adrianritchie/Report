using System.Text.Json;
using Microsoft.JSInterop;
using ReportGenerator.Web.Models;

namespace ReportGenerator.Web.Services;

/// <summary>
/// Persists example reports in browser localStorage so they survive page refreshes.
/// Serialises the list as JSON under the key <c>reportgen_examples</c>.
///
/// Note: localStorage is per-browser, so examples are shared across all sessions
/// on the same browser but not across different browsers or server restarts.
/// Unit tests are not provided for this service because it depends on
/// <see cref="IJSRuntime"/> which requires a Blazor host to test meaningfully.
/// </summary>
public sealed class ExampleReportsStore(IJSRuntime js)
{
    private const string StorageKey = "reportgen_examples";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Loads the saved examples from localStorage.
    /// Returns an empty list when nothing has been saved yet or the stored
    /// JSON is invalid.
    /// </summary>
    public async Task<List<ExampleReport>> LoadAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            return JsonSerializer.Deserialize<List<ExampleReport>>(json, JsonOptions) ?? [];
        }
        catch
        {
            // Corrupted data or JS interop not available (e.g. prerender) — start fresh.
            return [];
        }
    }

    /// <summary>
    /// Saves <paramref name="examples"/> to localStorage, replacing any
    /// previously saved value.
    /// </summary>
    public async Task SaveAsync(IReadOnlyList<ExampleReport> examples)
    {
        try
        {
            var json = JsonSerializer.Serialize(examples, JsonOptions);
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // Best-effort — if JS interop is unavailable (prerender) we skip silently.
        }
    }
}
