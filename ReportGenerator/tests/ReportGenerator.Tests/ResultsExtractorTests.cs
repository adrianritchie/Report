using ClosedXML.Excel;
using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

/// <summary>
/// Unit tests for <see cref="ResultsExtractor"/>.
/// Workbooks are built in-memory with ClosedXML and written to a temp .xlsx
/// file to exercise the full file-read path.
/// </summary>
public sealed class ResultsExtractorTests : IDisposable
{
    private readonly ResultsExtractor _extractor = new();
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

    /// <summary>
    /// Builds a minimal valid results workbook.
    ///
    /// Layout:
    ///   Row 1: [blank] [blank]  1     1     2
    ///   Row 2: [blank] [blank]  a     b     (blank)
    ///   Row 3: [blank] [blank]  3     4     5      | Total | % | Rank
    ///   Row 4: 12345   10A      2     4     5        85     72   4
    ///   Row 5: 67890   10B      1     0     3        45     38   12
    /// </summary>
    private static XLWorkbook BuildValidWorkbook()
    {
        var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Results");

        // Header row 1 — question numbers (cols 3+)
        ws.Cell(1, 3).Value = "1";
        ws.Cell(1, 4).Value = "1";
        ws.Cell(1, 5).Value = "2";
        // Last 3 cols headers (positional — not read by extractor but included for realism)
        ws.Cell(1, 6).Value = "Total";
        ws.Cell(1, 7).Value = "%";
        ws.Cell(1, 8).Value = "Rank";

        // Header row 2 — sub-parts
        ws.Cell(2, 3).Value = "a";
        ws.Cell(2, 4).Value = "b";
        // col 5 sub-part intentionally blank (no sub-part for Q2)

        // Header row 3 — max marks
        ws.Cell(3, 3).Value = 3;
        ws.Cell(3, 4).Value = 4;
        ws.Cell(3, 5).Value = 5;

        // Student 1
        ws.Cell(4, 1).Value = "12345";
        ws.Cell(4, 2).Value = "10A";
        ws.Cell(4, 3).Value = 2;
        ws.Cell(4, 4).Value = 4;
        ws.Cell(4, 5).Value = 5;
        ws.Cell(4, 6).Value = 85;
        ws.Cell(4, 7).Value = 72.5;
        ws.Cell(4, 8).Value = 4;

        // Student 2
        ws.Cell(5, 1).Value = "67890";
        ws.Cell(5, 2).Value = "10B";
        ws.Cell(5, 3).Value = 1;
        ws.Cell(5, 4).Value = 0;
        ws.Cell(5, 5).Value = 3;
        ws.Cell(5, 6).Value = 45;
        ws.Cell(5, 7).Value = 37.5;
        ws.Cell(5, 8).Value = 12;

        return wb;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Extract_ReturnsCorrectRows_ForValidSheet()
    {
        // Arrange
        using var wb = BuildValidWorkbook();
        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert — two students parsed correctly
        Assert.Equal(2, rows.Count);

        Assert.Equal("12345", rows[0].StudentNumber);
        Assert.Equal("10A",   rows[0].Class);
        Assert.Equal(3,       rows[0].Marks.Count);

        Assert.Equal("67890", rows[1].StudentNumber);
        Assert.Equal("10B",   rows[1].Class);
    }

    [Fact]
    public void Extract_BuildsCorrectQuestionLabels()
    {
        // Arrange
        using var wb = BuildValidWorkbook();
        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert — labels combine question number + sub-part where present
        Assert.Equal("1a", rows[0].Marks[0].Label);
        Assert.Equal("1b", rows[0].Marks[1].Label);
        Assert.Equal("2",  rows[0].Marks[2].Label);  // no sub-part
    }

    [Fact]
    public void Extract_ParsesStudentMarksAndMaxMarks()
    {
        // Arrange
        using var wb = BuildValidWorkbook();
        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert
        var mark = rows[0].Marks[0];   // Q1a
        Assert.Equal(2, mark.StudentMark);
        Assert.Equal(3, mark.MaxMark);
    }

    [Fact]
    public void Extract_SetsStudentMarkNull_ForBlankCell()
    {
        // Arrange — leave Q1b blank for student 2
        using var wb = BuildValidWorkbook();
        wb.Worksheets.First().Cell(5, 4).Value = "";   // student 2, Q1b
        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert
        Assert.Null(rows[1].Marks[1].StudentMark);   // Q1b not attempted
    }

    [Fact]
    public void Extract_ParsesTotalPercentageAndRank()
    {
        // Arrange
        using var wb = BuildValidWorkbook();
        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert student 1
        Assert.Equal(85, rows[0].Total);
        Assert.Equal(72.5, rows[0].Percentage);
        Assert.Equal(4, rows[0].Rank);

        // Assert student 2
        Assert.Equal(45, rows[1].Total);
        Assert.Equal(37.5, rows[1].Percentage);
        Assert.Equal(12, rows[1].Rank);
    }

    [Fact]
    public void Extract_SetsNullSummaryFields_WhenSummaryCellsAreBlank()
    {
        // Arrange — clear the summary columns for student 1
        using var wb = BuildValidWorkbook();
        var ws = wb.Worksheets.First();
        ws.Cell(4, 6).Value = "";
        ws.Cell(4, 7).Value = "";
        ws.Cell(4, 8).Value = "";
        var path = SaveToTempFile(wb);

        // Act
        var rows = _extractor.Extract(path);

        // Assert
        Assert.Null(rows[0].Total);
        Assert.Null(rows[0].Percentage);
        Assert.Null(rows[0].Rank);
    }

    [Fact]
    public void Extract_Throws_FileNotFoundException_WhenFileMissing()
    {
        var ex = Assert.Throws<FileNotFoundException>(
            () => _extractor.Extract("/nonexistent/path/missing.xlsx"));

        Assert.Contains("missing.xlsx", ex.Message);
    }

    [Fact]
    public void Extract_Throws_InvalidOperationException_WhenNoStudentRows()
    {
        // Arrange — only 3 header rows, no student data
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Results");
        ws.Cell(1, 3).Value = "1";
        ws.Cell(2, 3).Value = "a";
        ws.Cell(3, 3).Value = 5;
        // Last 3 cols to make the column count valid
        ws.Cell(1, 4).Value = "Total";
        ws.Cell(1, 5).Value = "%";
        ws.Cell(1, 6).Value = "Rank";
        var path = SaveToTempFile(wb);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _extractor.Extract(path));
    }
}
