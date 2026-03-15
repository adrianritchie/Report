using ClosedXML.Excel;
using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

/// <summary>
/// Unit tests for <see cref="ExcelExtractor"/>.
/// Workbooks are built in-memory with ClosedXML and written to a temp .xlsx
/// file to exercise the full file-read path.
/// </summary>
public sealed class ExcelExtractorTests : IDisposable
{
    private readonly ExcelExtractor _extractor = new();
    private readonly List<string> _tempFiles = [];

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the workbook to a temp .xlsx file and returns its path.
    /// The file is cleaned up in Dispose().
    /// </summary>
    private string SaveToTempFile(XLWorkbook workbook)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        workbook.SaveAs(path);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Extract_ReturnsStudentRows_ForValidSheet()
    {
        // Arrange
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Feedback");

        // Headings
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Written Communication";
        ws.Cell(1, 3).Value = "Mathematical Reasoning";

        // Student 1
        ws.Cell(2, 1).Value = "Alice Smith";
        ws.Cell(2, 2).Value = "Excellent work";
        ws.Cell(2, 3).Value = "Needs improvement";

        // Student 2
        ws.Cell(3, 1).Value = "Bob Jones";
        ws.Cell(3, 2).Value = "Good effort";
        ws.Cell(3, 3).Value = "Strong performance";

        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert
        Assert.Equal(2, rows.Count);

        Assert.Equal("Alice Smith", rows[0].Name);
        Assert.Equal("Alice Smith", rows[0].FullName);
        Assert.Equal(2, rows[0].Fields.Count);
        Assert.Equal("Written Communication", rows[0].Fields[0].Heading);
        Assert.Equal("Excellent work",        rows[0].Fields[0].Value);
        Assert.Equal("Mathematical Reasoning", rows[0].Fields[1].Heading);
        Assert.Equal("Needs improvement",      rows[0].Fields[1].Value);

        Assert.Equal("Bob Jones", rows[1].Name);
        Assert.Equal("Bob Jones", rows[1].FullName);
    }

    [Fact]
    public void Extract_SkipsEmptyRows()
    {
        // Arrange
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Feedback");

        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Comments";

        ws.Cell(2, 1).Value = "Alice Smith";
        ws.Cell(2, 2).Value = "Great";

        // Row 3 intentionally left blank

        ws.Cell(4, 1).Value = "Bob Jones";
        ws.Cell(4, 2).Value = "Good";

        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert — only 2 real students, blank row skipped
        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice Smith", rows[0].Name);
        Assert.Equal("Bob Jones",   rows[1].Name);
    }

    [Fact]
    public void Extract_ThrowsFileNotFoundException_WhenFileMissing()
    {
        var ex = Assert.Throws<FileNotFoundException>(
            () => _extractor.Extract("/nonexistent/path/missing.xlsx"));

        Assert.Contains("missing.xlsx", ex.Message);
    }

    [Fact]
    public void Extract_ThrowsInvalidOperationException_WhenNoDataRows()
    {
        // Arrange — heading row only, no student rows
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Feedback");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Comments";

        var path = SaveToTempFile(wb);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _extractor.Extract(path));
    }

    [Fact]
    public void Extract_ThrowsInvalidOperationException_WhenNoColumns()
    {
        // Arrange — completely empty sheet
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Feedback");

        var path = SaveToTempFile(wb);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _extractor.Extract(path));
    }

    [Fact]
    public void Extract_IncludesHeading_WhenFeedbackCellIsBlank()
    {
        // Arrange
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Feedback");

        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Written Communication";
        ws.Cell(1, 3).Value = "Mathematical Reasoning";

        ws.Cell(2, 1).Value = "Alice Smith";
        // Column 2 left blank
        ws.Cell(2, 3).Value = "Strong";

        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert — both headings present; blank cell → empty string value
        Assert.Single(rows);
        Assert.Equal(2, rows[0].Fields.Count);
        Assert.Equal("Written Communication", rows[0].Fields[0].Heading);
        Assert.Equal(string.Empty,            rows[0].Fields[0].Value);
        Assert.Equal("Mathematical Reasoning", rows[0].Fields[1].Heading);
        Assert.Equal("Strong",                 rows[0].Fields[1].Value);
    }
}
