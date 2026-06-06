using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

/// <summary>
/// Unit tests for <see cref="DocxExtractor"/>, covering both the static
/// <see cref="DocxExtractor.StripDocx"/> logic and the public async API.
/// </summary>
public sealed class DocxExtractorTests : IDisposable
{
    private readonly DocxExtractor _extractor = new();
    private readonly List<string> _tempFiles = [];

    // Word processing ML namespace.
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// Builds a minimal but valid .docx byte payload containing the given paragraphs.
    /// Each string in <paramref name="paragraphs"/> becomes one &lt;w:p&gt; element.
    /// </summary>
    private static byte[] BuildDocx(params string[] paragraphs)
    {
        // Construct the document.xml body.
        var body = new XElement(W + "body",
            paragraphs.Select(text =>
                new XElement(W + "p",
                    new XElement(W + "r",
                        new XElement(W + "t",
                            new XAttribute(XNamespace.Xml + "space", "preserve"),
                            text)))));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(W + "document", body));

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var entryStream = entry.Open();
            document.Save(entryStream);
        }

        return ms.ToArray();
    }

    private string WriteTempDocx(params string[] paragraphs)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        File.WriteAllBytes(path, BuildDocx(paragraphs));
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // ── StripDocx unit tests ──────────────────────────────────────────────────

    [Fact]
    public void StripDocx_ExtractsText_FromSingleParagraph()
    {
        var bytes = BuildDocx("Hello World");
        var result = DocxExtractor.StripDocx(bytes);
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public void StripDocx_PreservesParagraphBreaks()
    {
        var bytes = BuildDocx("Line one", "Line two", "Line three");
        var result = DocxExtractor.StripDocx(bytes);
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, l => l.Contains("Line one"));
        Assert.Contains(lines, l => l.Contains("Line two"));
        Assert.Contains(lines, l => l.Contains("Line three"));
    }

    [Fact]
    public void StripDocx_CollapsesExcessBlankLines()
    {
        // Four empty paragraphs in a row should collapse to at most two newlines.
        var bytes = BuildDocx("First", "", "", "", "", "Second");
        var result = DocxExtractor.StripDocx(bytes);
        Assert.DoesNotContain("\n\n\n", result);
        Assert.Contains("First", result);
        Assert.Contains("Second", result);
    }

    [Fact]
    public void StripDocx_HandlesMultipleRunsInOneParagraph()
    {
        // Build a paragraph with two <w:r> runs manually.
        var body = new XElement(W + "body",
            new XElement(W + "p",
                new XElement(W + "r", new XElement(W + "t", "Hello ")),
                new XElement(W + "r", new XElement(W + "t",
                    new XAttribute(XNamespace.Xml + "space", "preserve"), "World"))));

        var doc = new XDocument(new XElement(W + "document", body));

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var stream = entry.Open();
            doc.Save(stream);
        }

        var result = DocxExtractor.StripDocx(ms.ToArray());
        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }

    [Fact]
    public void StripDocx_Throws_WhenNotAZip()
    {
        var bytes = Encoding.UTF8.GetBytes("This is not a ZIP file at all.");
        var ex = Assert.Throws<InvalidOperationException>(
            () => DocxExtractor.StripDocx(bytes, "test.docx"));
        Assert.Contains("test.docx", ex.Message);
        Assert.Contains("password-protected", ex.Message);
    }

    [Fact]
    public void StripDocx_Throws_WhenZipLacksDocumentXml()
    {
        // Valid ZIP but missing word/document.xml.
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("not-word/other.xml");
            using var s = entry.Open();
            s.WriteByte(0x20);
        }

        var ex = Assert.Throws<InvalidOperationException>(
            () => DocxExtractor.StripDocx(ms.ToArray(), "bad.docx"));
        Assert.Contains("word/document.xml", ex.Message);
    }

    // ── ExtractAsync API tests ────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_ReturnsText_FromValidDocxFile()
    {
        var path = WriteTempDocx("Q1. Describe osmosis.", "Q2. Explain photosynthesis.");
        var result = await _extractor.ExtractAsync(path);
        Assert.Contains("Q1", result);
        Assert.Contains("osmosis", result);
        Assert.Contains("Q2", result);
    }

    [Fact]
    public async Task ExtractAsync_Throws_FileNotFoundException_WhenMissing()
    {
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => _extractor.ExtractAsync("/nonexistent/path/missing.docx"));
        Assert.Contains("missing.docx", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_Throws_InvalidOperationException_WhenNotZip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        _tempFiles.Add(path);
        await File.WriteAllTextAsync(path, "This is plain text, not a docx.");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _extractor.ExtractAsync(path));
    }

    [Fact]
    public async Task ExtractAsync_Throws_InvalidOperationException_WhenEmptyDocument()
    {
        // Valid ZIP + valid XML but no <w:t> text content.
        var body = new XElement(W + "body", new XElement(W + "p"));
        var doc = new XDocument(new XElement(W + "document", body));

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var stream = entry.Open();
            doc.Save(stream);
        }

        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
        _tempFiles.Add(path);
        await File.WriteAllBytesAsync(path, ms.ToArray());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _extractor.ExtractAsync(path));
    }

    [Fact]
    public async Task ExtractAsync_RunsThroughExamTextCleaner()
    {
        // ExamTextCleaner strips lines like "DO NOT WRITE IN THIS AREA".
        var path = WriteTempDocx("DO NOT WRITE IN THIS AREA", "Q1. Describe osmosis.");
        var result = await _extractor.ExtractAsync(path);
        Assert.DoesNotContain("DO NOT WRITE IN THIS AREA", result);
        Assert.Contains("Q1", result);
    }
}
