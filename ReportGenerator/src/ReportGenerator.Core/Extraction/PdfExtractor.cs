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

        var pages = document.GetPages().ToList();

        // Skip the first page (cover / title page).
        foreach (var page in pages.Skip(1))
        {
            var cleaned = ExamTextCleaner.Clean(page.Text);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                sb.AppendLine(cleaned);
            }
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException(
                $"No text could be extracted from PDF: {filePath}. " +
                "The file may be scanned/image-based or contain only a cover page. " +
                "Please use a text-based PDF or provide a .txt file.");

        return result;
    }
}
