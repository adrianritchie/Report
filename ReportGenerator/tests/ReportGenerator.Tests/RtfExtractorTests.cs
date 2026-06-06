using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

/// <summary>
/// Unit tests for <see cref="RtfExtractor"/>, covering both the internal
/// <see cref="RtfExtractor.StripRtf"/> logic and the public async API.
/// </summary>
public sealed class RtfExtractorTests : IDisposable
{
    private readonly RtfExtractor _extractor = new();
    private readonly List<string> _tempFiles = [];

    // ── helpers ───────────────────────────────────────────────────────────────

    private string WriteTempFile(string content, string extension = ".rtf")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(path, content, System.Text.Encoding.ASCII);
        _tempFiles.Add(path);
        return path;
    }

    private static string MinimalRtf(string body)
        => @"{\rtf1\ansi " + body + "}";

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // ── StripRtf unit tests ───────────────────────────────────────────────────

    [Fact]
    public void StripRtf_ExtractsPlainText_FromSimpleDocument()
    {
        var rtf = MinimalRtf(@"\b Hello\b0 World\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }

    [Fact]
    public void StripRtf_ConvertsParToNewline()
    {
        var rtf = MinimalRtf(@"Line one\par Line two\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("Line one", result);
        Assert.Contains("Line two", result);
        // They should be on separate lines.
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, l => l.Contains("Line one"));
        Assert.Contains(lines, l => l.Contains("Line two"));
    }

    [Fact]
    public void StripRtf_RemovesPictGroups()
    {
        // A \pict group with fake hex data should be entirely removed.
        var rtf = MinimalRtf(@"Before{\pict\pngblip\picw100\pich100 89504e470d0a}After\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("Before", result);
        Assert.Contains("After", result);
        Assert.DoesNotContain("pict", result);
        Assert.DoesNotContain("89504e47", result);
    }

    [Fact]
    public void StripRtf_DecodesDegreeSymbol()
    {
        // \'b0 is the degree symbol in Windows-1252.
        var rtf = MinimalRtf(@"37 \'b0C\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("37", result);
        Assert.Contains("°", result);
    }

    [Fact]
    public void StripRtf_DecodesPercentSign()
    {
        // \'25 is '%' in Windows-1252.
        var rtf = MinimalRtf(@"80\'25 of students\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("80%", result);
        Assert.Contains("of students", result);
    }

    [Fact]
    public void StripRtf_RemovesControlWords()
    {
        // Control words like \f1, \fs22, \cf0, \ltrpar should not appear in output.
        var rtf = MinimalRtf(@"\f1\fs22\cf0\ltrpar Question text\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("Question text", result);
        Assert.DoesNotContain(@"\f1", result);
        Assert.DoesNotContain(@"\fs22", result);
        Assert.DoesNotContain(@"\cf0", result);
    }

    [Fact]
    public void StripRtf_RemovesFontTable()
    {
        var rtf = MinimalRtf(
            @"{\fonttbl{\f0\fnil\fcharset0 Times New Roman;}{\f1\fnil Arial;}}" +
            @"Actual content\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("Actual content", result);
        Assert.DoesNotContain("Times New Roman", result);
        Assert.DoesNotContain("fonttbl", result);
    }

    [Fact]
    public void StripRtf_RemovesColourTable()
    {
        var rtf = MinimalRtf(
            @"{\colortbl;\red0\green0\blue0;\red255\green0\blue0;}" +
            @"Coloured text\par");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("Coloured text", result);
        Assert.DoesNotContain("colortbl", result);
    }

    [Fact]
    public void StripRtf_HandlesTableCellsAsSpaces()
    {
        // \cell should become a space so adjacent cell content is separated.
        var rtf = MinimalRtf(@"\intbl Cell A\cell Cell B\cell\row\pard");
        var result = RtfExtractor.StripRtf(rtf);
        Assert.Contains("Cell A", result);
        Assert.Contains("Cell B", result);
    }

    [Fact]
    public void StripRtf_CollapsesMultipleBlankLines()
    {
        var rtf = MinimalRtf(@"First\par\par\par\par Second\par");
        var result = RtfExtractor.StripRtf(rtf);
        // Should not have more than one consecutive blank line.
        Assert.DoesNotContain("\n\n\n", result);
        Assert.Contains("First", result);
        Assert.Contains("Second", result);
    }

    // ── ExtractAsync API tests ────────────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_ReturnsText_FromValidRtfFile()
    {
        var path = WriteTempFile(MinimalRtf(@"Q1. Describe osmosis.\par"));
        var result = await _extractor.ExtractAsync(path);
        Assert.Contains("Q1", result);
        Assert.Contains("osmosis", result);
    }

    [Fact]
    public async Task ExtractAsync_WorksWithDocExtension()
    {
        // .doc files that are actually RTF should be accepted.
        var path = WriteTempFile(MinimalRtf(@"Question content\par"), ".doc");
        var result = await _extractor.ExtractAsync(path);
        Assert.Contains("Question content", result);
    }

    [Fact]
    public async Task ExtractAsync_Throws_FileNotFoundException_WhenFileMissing()
    {
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(
            () => _extractor.ExtractAsync("/nonexistent/path/missing.rtf"));
        Assert.Contains("missing.rtf", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_Throws_InvalidOperationException_WhenNotRtf()
    {
        // A plain text file with a .rtf extension should be rejected.
        var path = WriteTempFile("This is not an RTF file at all.");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _extractor.ExtractAsync(path));
    }

    [Fact]
    public async Task ExtractAsync_Throws_InvalidOperationException_WhenOnlyImages()
    {
        // A document whose text content is entirely empty after stripping.
        var rtf = @"{\rtf1\ansi {\pict\pngblip\picw100\pich100 89504e470d0a}}";
        var path = WriteTempFile(rtf);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _extractor.ExtractAsync(path));
    }

    [Fact]
    public async Task ExtractAsync_RunsThroughExamTextCleaner()
    {
        // Boilerplate lines that ExamTextCleaner removes should not appear in output.
        var rtf = MinimalRtf(@"DO NOT WRITE IN THIS AREA\par Q1. Describe photosynthesis.\par");
        var path = WriteTempFile(rtf);
        var result = await _extractor.ExtractAsync(path);
        Assert.DoesNotContain("DO NOT WRITE IN THIS AREA", result);
        Assert.Contains("Q1", result);
    }
}
