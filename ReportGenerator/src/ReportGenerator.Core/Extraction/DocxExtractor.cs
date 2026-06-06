using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ReportGenerator.Extraction;

/// <summary>
/// Extracts plain text from Open XML Word documents (<c>.docx</c>).
///
/// A <c>.docx</c> file is a ZIP archive.  This extractor:
/// <list type="number">
///   <item>Opens the ZIP and locates <c>word/document.xml</c>.</item>
///   <item>Walks <c>&lt;w:p&gt;</c> paragraph elements in document order.</item>
///   <item>Joins <c>&lt;w:t&gt;</c> run text within each paragraph, respecting
///         <c>xml:space="preserve"</c> for leading/trailing spaces.</item>
///   <item>Emits a newline between paragraphs.</item>
///   <item>Collapses excess blank lines and passes the result through
///         <see cref="ExamTextCleaner"/>.</item>
/// </list>
/// </summary>
public sealed class DocxExtractor : IContentExtractor
{
    // Word processing ML namespace used throughout Open XML documents.
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    // xml:space attribute name.
    private static readonly XName XmlSpace = XNamespace.Xml + "space";

    public async Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Exam file not found: {filePath}", filePath);

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var text = StripDocx(bytes, Path.GetFileName(filePath));

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                $"No text could be extracted from '{Path.GetFileName(filePath)}'. " +
                "The file may be empty or contain only images.");

        return ExamTextCleaner.Clean(text);
    }

    /// <summary>
    /// Extracts plain text from a <c>.docx</c> byte payload.
    /// Public and static so tests can call it directly without writing temp files.
    /// </summary>
    /// <param name="bytes">Raw bytes of the <c>.docx</c> ZIP archive.</param>
    /// <param name="displayName">File name used in error messages (optional).</param>
    public static string StripDocx(byte[] bytes, string displayName = "document.docx")
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException(
                $"'{displayName}' could not be opened as a Word document. " +
                "It may be password-protected, corrupt, or not a .docx file.");
        }

        using (zip)
        {
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null)
                throw new InvalidOperationException(
                    $"'{displayName}' does not contain 'word/document.xml'. " +
                    "It may not be a valid Word document.");

            XDocument doc;
            using (var stream = entry.Open())
                doc = XDocument.Load(stream);

            return ExtractText(doc);
        }
    }

    private static string ExtractText(XDocument doc)
    {
        var paragraphs = doc.Descendants(W + "p");
        var lines = new List<string>();

        foreach (var para in paragraphs)
        {
            var sb = new System.Text.StringBuilder();

            foreach (var run in para.Descendants(W + "t"))
            {
                // xml:space="preserve" means we must keep the text verbatim (including
                // leading/trailing spaces).  Without it XDocument already normalises
                // whitespace, so we just append the value.
                var value = run.Value;
                if (run.Attribute(XmlSpace)?.Value == "preserve")
                    sb.Append(value);
                else
                    sb.Append(value);
            }

            lines.Add(sb.ToString());
        }

        // Join paragraphs with newlines, then collapse 3+ consecutive blank lines to 2.
        var text = string.Join("\n", lines);
        text = Regex.Replace(text, @"(\r?\n){3,}", "\n\n");

        return text.Trim();
    }
}
