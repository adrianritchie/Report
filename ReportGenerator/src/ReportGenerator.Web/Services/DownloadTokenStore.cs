using System.Collections.Concurrent;

namespace ReportGenerator.Web.Services;

/// <summary>
/// Stores a mapping from a short-lived GUID token to a server-side file path,
/// allowing the browser to download a generated report without exposing the path.
/// </summary>
public sealed class DownloadTokenStore
{
    private readonly ConcurrentDictionary<string, string> _map = new();

    /// <summary>
    /// Registers <paramref name="filePath"/> and returns a new GUID token.
    /// </summary>
    public string Register(string filePath)
    {
        var token = Guid.NewGuid().ToString("N");
        _map[token] = filePath;
        return token;
    }

    /// <summary>
    /// Resolves a token back to its file path.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the token is not found.</exception>
    public string Resolve(string token)
    {
        if (_map.TryGetValue(token, out var path))
            return path;

        throw new KeyNotFoundException($"Download token '{token}' not found or already used.");
    }
}
