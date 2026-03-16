namespace ReportGenerator.Extraction;

public sealed class TextFileExtractor : IContentExtractor
{
    public async Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Text file not found: {filePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException($"File is empty: {filePath}");

        return ExamTextCleaner.Clean(content);
    }
}
