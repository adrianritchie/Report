namespace ReportGenerator.Extraction;

/// <summary>
/// Routes extraction to either <see cref="PdfExtractor"/> or <see cref="TextFileExtractor"/>
/// based on file extension.
/// </summary>
public sealed class ContentExtractorRouter : IContentExtractor
{
    private readonly PdfExtractor _pdfExtractor = new();
    private readonly TextFileExtractor _textExtractor = new();

    public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => _pdfExtractor.ExtractAsync(filePath, cancellationToken),
            ".txt" or ".md" or ".text" => _textExtractor.ExtractAsync(filePath, cancellationToken),
            _ => throw new NotSupportedException(
                $"Unsupported file type '{extension}'. Supported formats: .pdf, .txt")
        };
    }
}
