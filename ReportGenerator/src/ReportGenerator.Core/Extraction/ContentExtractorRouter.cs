namespace ReportGenerator.Extraction;

/// <summary>
/// Routes extraction to <see cref="PdfExtractor"/>, <see cref="TextFileExtractor"/>,
/// <see cref="RtfExtractor"/>, or <see cref="DocxExtractor"/> based on file extension.
/// </summary>
public sealed class ContentExtractorRouter : IContentExtractor
{
    private readonly PdfExtractor _pdfExtractor = new();
    private readonly TextFileExtractor _textExtractor = new();
    private readonly RtfExtractor _rtfExtractor = new();
    private readonly DocxExtractor _docxExtractor = new();

    public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => _pdfExtractor.ExtractAsync(filePath, cancellationToken),
            ".txt" or ".md" or ".text" => _textExtractor.ExtractAsync(filePath, cancellationToken),
            ".doc" or ".rtf" => _rtfExtractor.ExtractAsync(filePath, cancellationToken),
            ".docx" => _docxExtractor.ExtractAsync(filePath, cancellationToken),
            _ => throw new NotSupportedException(
                $"Unsupported file type '{extension}'. Supported formats: .pdf, .txt, .doc, .rtf, .docx")
        };
    }
}
