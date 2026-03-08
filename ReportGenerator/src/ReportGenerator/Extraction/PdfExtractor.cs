using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ReportGenerator.Extraction;

public sealed class PdfExtractor : IContentExtractor
{
    public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF file not found: {filePath}", filePath);

        var text = ExtractText(filePath);
        return Task.FromResult(text);
    }

    private static string ExtractText(string filePath)
    {
        using var document = PdfDocument.Open(filePath);
        var sb = new System.Text.StringBuilder();

        foreach (Page page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException(
                $"No text could be extracted from PDF: {filePath}. " +
                "The file may be scanned/image-based. Please use a text-based PDF or provide a .txt file.");

        return result;
    }
}
