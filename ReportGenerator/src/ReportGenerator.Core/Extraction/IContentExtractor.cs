namespace ReportGenerator.Extraction;

public interface IContentExtractor
{
    /// <summary>Extracts plain text from a PDF or text file.</summary>
    /// <param name="filePath">Absolute or relative path to the file.</param>
    /// <returns>Extracted text content.</returns>
    Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default);
}
