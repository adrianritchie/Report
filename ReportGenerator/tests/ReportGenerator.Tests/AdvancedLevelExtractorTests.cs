using ClosedXML.Excel;
using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

/// <summary>
/// Unit tests for <see cref="AdvancedLevelExtractor"/>.
/// Workbooks are built in-memory with ClosedXML and written to a temp .xlsx
/// file to exercise the full file-read path.
/// </summary>
public sealed class AdvancedLevelExtractorTests : IDisposable
{
    private readonly AdvancedLevelExtractor _extractor = new();
    private readonly List<string> _tempFiles = [];

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

    // ── Standard layout (headers in rows 1-2, data from row 3) ──────────────

    [Fact]
    public void Extract_StandardLayout_ReturnsCorrectTopics()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        // Row 1: Topic names
        ws.Cell(1, 2).Value = "Biochemistry";
        ws.Cell(1, 5).Value = "Exam";

        // Row 2: Sub-headers
        ws.Cell(2, 2).Value = "M"; ws.Cell(2, 3).Value = "%"; ws.Cell(2, 4).Value = "G";
        ws.Cell(2, 5).Value = "M"; ws.Cell(2, 6).Value = "%"; ws.Cell(2, 7).Value = "G";

        // Row 3: Student data
        ws.Cell(3, 1).Value = "1";
        ws.Cell(3, 2).Value = "17"; ws.Cell(3, 3).Value = "42.5"; ws.Cell(3, 4).Value = "D";
        ws.Cell(3, 5).Value = "30"; ws.Cell(3, 6).Value = "75.0"; ws.Cell(3, 7).Value = "A";

        ws.Cell(4, 1).Value = "2";
        ws.Cell(4, 2).Value = "31"; ws.Cell(4, 3).Value = "77.5"; ws.Cell(4, 4).Value = "A*";
        ws.Cell(4, 5).Value = "40"; ws.Cell(4, 6).Value = "85.0"; ws.Cell(4, 7).Value = "A*";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Equal(2, rows.Count);

        Assert.Equal("1", rows[0].StudentNumber);
        Assert.Single(rows[0].Topics);
        Assert.Equal("Biochemistry", rows[0].Topics[0].TopicName);
        Assert.Equal(17, rows[0].Topics[0].Mark);
        Assert.Equal(42.5, rows[0].Topics[0].Percentage);
        Assert.Equal("D", rows[0].Topics[0].Grade);
        Assert.Equal(30, rows[0].Exam.Mark);
        Assert.Equal(75.0, rows[0].Exam.Percentage);
        Assert.Equal("A", rows[0].Exam.Grade);

        Assert.Equal("2", rows[1].StudentNumber);
        Assert.Equal(31, rows[1].Topics[0].Mark);
        Assert.Equal(40, rows[1].Exam.Mark);
    }

    [Fact]
    public void Extract_StandardLayout_WithAverageColumn()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        ws.Cell(1, 2).Value = "Topic1";
        ws.Cell(1, 5).Value = "Av";

        ws.Cell(2, 2).Value = "M"; ws.Cell(2, 3).Value = "%"; ws.Cell(2, 4).Value = "G";
        ws.Cell(2, 5).Value = "%"; ws.Cell(2, 6).Value = "G";

        ws.Cell(3, 1).Value = "1";
        ws.Cell(3, 2).Value = "20"; ws.Cell(3, 3).Value = "50.0"; ws.Cell(3, 4).Value = "C";
        ws.Cell(3, 5).Value = "55.0"; ws.Cell(3, 6).Value = "B";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Single(rows);
        Assert.Equal(55.0, rows[0].AveragePercentage);
        Assert.Equal("B", rows[0].OverallGrade);
    }

    // ── Offset layout (empty row 1, headers in rows 2-3, data from row 4) ───

    [Fact]
    public void Extract_OffsetLayout_ReturnsCorrectTopics()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        // Row 1: empty
        // Row 2: Topic names (with merged cells in real file)
        ws.Cell(2, 2).Value = "Biochemistry";
        ws.Cell(2, 5).Value = "Exam";

        // Row 3: Sub-headers
        ws.Cell(3, 2).Value = "M"; ws.Cell(3, 3).Value = "%"; ws.Cell(3, 4).Value = "G";
        ws.Cell(3, 5).Value = "M"; ws.Cell(3, 6).Value = "%"; ws.Cell(3, 7).Value = "G";

        // Row 4: Student data
        ws.Cell(4, 1).Value = "1";
        ws.Cell(4, 2).Value = "17"; ws.Cell(4, 3).Value = "42.5"; ws.Cell(4, 4).Value = "D";
        ws.Cell(4, 5).Value = "30"; ws.Cell(4, 6).Value = "75.0"; ws.Cell(4, 7).Value = "A";

        ws.Cell(5, 1).Value = "2";
        ws.Cell(5, 2).Value = "31"; ws.Cell(5, 3).Value = "77.5"; ws.Cell(5, 4).Value = "A*";
        ws.Cell(5, 5).Value = "40"; ws.Cell(5, 6).Value = "85.0"; ws.Cell(5, 7).Value = "A*";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Equal(2, rows.Count);

        Assert.Equal("1", rows[0].StudentNumber);
        Assert.Single(rows[0].Topics);
        Assert.Equal("Biochemistry", rows[0].Topics[0].TopicName);
        Assert.Equal(17, rows[0].Topics[0].Mark);
        Assert.Equal(42.5, rows[0].Topics[0].Percentage);
        Assert.Equal("D", rows[0].Topics[0].Grade);
        Assert.Equal(30, rows[0].Exam.Mark);
        Assert.Equal(75.0, rows[0].Exam.Percentage);
        Assert.Equal("A", rows[0].Exam.Grade);

        Assert.Equal("2", rows[1].StudentNumber);
        Assert.Equal(31, rows[1].Topics[0].Mark);
        Assert.Equal(40, rows[1].Exam.Mark);
    }

    [Fact]
    public void Extract_OffsetLayout_WithAverageColumn()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        ws.Cell(2, 2).Value = "Topic1";
        ws.Cell(2, 5).Value = "Av";

        ws.Cell(3, 2).Value = "M"; ws.Cell(3, 3).Value = "%"; ws.Cell(3, 4).Value = "G";
        ws.Cell(3, 5).Value = "%"; ws.Cell(3, 6).Value = "G";

        ws.Cell(4, 1).Value = "1";
        ws.Cell(4, 2).Value = "20"; ws.Cell(4, 3).Value = "50.0"; ws.Cell(4, 4).Value = "C";
        ws.Cell(4, 5).Value = "55.0"; ws.Cell(4, 6).Value = "B";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Single(rows);
        Assert.Equal(55.0, rows[0].AveragePercentage);
        Assert.Equal("B", rows[0].OverallGrade);
    }

    [Fact]
    public void Extract_OffsetLayout_WithMergedTopicHeaders()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        // Merged topic names spanning 3 columns each
        ws.Range(2, 2, 2, 4).Merge();
        ws.Cell(2, 2).Value = "Biochemistry";
        ws.Range(2, 5, 2, 7).Merge();
        ws.Cell(2, 5).Value = "Biochem 2";

        ws.Cell(3, 2).Value = "M"; ws.Cell(3, 3).Value = "%"; ws.Cell(3, 4).Value = "G";
        ws.Cell(3, 5).Value = "M"; ws.Cell(3, 6).Value = "%"; ws.Cell(3, 7).Value = "G";

        ws.Cell(4, 1).Value = "1";
        ws.Cell(4, 2).Value = "17"; ws.Cell(4, 3).Value = "42.5"; ws.Cell(4, 4).Value = "D";
        ws.Cell(4, 5).Value = "22"; ws.Cell(4, 6).Value = "37.9"; ws.Cell(4, 7).Value = "D";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Single(rows);
        Assert.Equal(2, rows[0].Topics.Count);
        Assert.Equal("Biochemistry", rows[0].Topics[0].TopicName);
        Assert.Equal("Biochem 2", rows[0].Topics[1].TopicName);
        Assert.Equal(17, rows[0].Topics[0].Mark);
        Assert.Equal(22, rows[0].Topics[1].Mark);
    }

    // ── Error cases ──────────────────────────────────────────────────────────

    [Fact]
    public void Extract_ThrowsFileNotFoundException_WhenFileMissing()
    {
        Assert.Throws<FileNotFoundException>(
            () => _extractor.Extract("/nonexistent/path/missing.xlsx"));
    }

    [Fact]
    public void Extract_ThrowsInvalidOperationException_WhenNoSubHeaderRow()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(2, 1).Value = "Alice";

        var path = SaveToTempFile(wb);
        Assert.Throws<InvalidOperationException>(() => _extractor.Extract(path));
    }

    [Fact]
    public void Extract_ThrowsInvalidOperationException_WhenNoDataRows()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        ws.Cell(1, 2).Value = "Topic1";
        ws.Cell(2, 2).Value = "M"; ws.Cell(2, 3).Value = "%"; ws.Cell(2, 4).Value = "G";

        var path = SaveToTempFile(wb);
        Assert.Throws<InvalidOperationException>(() => _extractor.Extract(path));
    }

    [Fact]
    public void Extract_SkipsEmptyStudentNumberRows()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        ws.Cell(1, 2).Value = "Topic1";
        ws.Cell(2, 2).Value = "M"; ws.Cell(2, 3).Value = "%"; ws.Cell(2, 4).Value = "G";

        ws.Cell(3, 1).Value = "1";
        ws.Cell(3, 2).Value = "20"; ws.Cell(3, 3).Value = "50.0"; ws.Cell(3, 4).Value = "C";

        // Row 4: empty student number — should be skipped
        ws.Cell(4, 1).Value = "";
        ws.Cell(4, 2).Value = "30"; ws.Cell(4, 3).Value = "60.0"; ws.Cell(4, 4).Value = "B";

        ws.Cell(5, 1).Value = "2";
        ws.Cell(5, 2).Value = "25"; ws.Cell(5, 3).Value = "55.0"; ws.Cell(5, 4).Value = "B+";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Equal(2, rows.Count);
        Assert.Equal("1", rows[0].StudentNumber);
        Assert.Equal("2", rows[1].StudentNumber);
    }

    // ── Match the real file layout (L6 test marks.xlsx) ──────────────────────

    [Fact]
    public void Extract_MatchesRealL6TestMarksLayout()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        // Row 1: empty (matches the real file)
        // Row 2: Topic names with merged ranges
        ws.Range(2, 2, 2, 4).Merge();
        ws.Cell(2, 2).Value = "Biochemistry";
        ws.Range(2, 5, 2, 7).Merge();
        ws.Cell(2, 5).Value = "Biochem 2";
        ws.Range(2, 8, 2, 10).Merge();
        ws.Cell(2, 8).Value = "cells";
        // Cols 11-13: no header (exam — unnamed)
        ws.Range(2, 14, 2, 15).Merge();
        ws.Cell(2, 14).Value = "Av";

        // Row 3: Sub-headers
        ws.Cell(3, 2).Value = "M"; ws.Cell(3, 3).Value = "%"; ws.Cell(3, 4).Value = "G";
        ws.Cell(3, 5).Value = "M"; ws.Cell(3, 6).Value = "%"; ws.Cell(3, 7).Value = "G";
        ws.Cell(3, 8).Value = "M"; ws.Cell(3, 9).Value = "%"; ws.Cell(3, 10).Value = "G";
        // Cols 11-13: no sub-headers (exam — unnamed)
        ws.Cell(3, 14).Value = "%"; ws.Cell(3, 15).Value = "G";

        // Row 4: Student 1
        ws.Cell(4, 1).Value = "1";
        ws.Cell(4, 2).Value = "17"; ws.Cell(4, 3).Value = "42.5"; ws.Cell(4, 4).Value = "D";
        ws.Cell(4, 5).Value = "22"; ws.Cell(4, 6).Value = "37.9"; ws.Cell(4, 7).Value = "D";
        ws.Cell(4, 8).Value = "12"; ws.Cell(4, 9).Value = "19.7"; ws.Cell(4, 10).Value = "";
        ws.Cell(4, 11).Value = "23"; ws.Cell(4, 12).Value = "25.55"; ws.Cell(4, 13).Value = "E";
        ws.Cell(4, 14).Value = "33.4"; ws.Cell(4, 15).Value = "E/D";

        // Row 5: Student 2
        ws.Cell(5, 1).Value = "2";
        ws.Cell(5, 2).Value = "31"; ws.Cell(5, 3).Value = "77.5"; ws.Cell(5, 4).Value = "A*";
        ws.Cell(5, 5).Value = "41"; ws.Cell(5, 6).Value = "70.7"; ws.Cell(5, 7).Value = "A";
        ws.Cell(5, 8).Value = "46"; ws.Cell(5, 9).Value = "75.4"; ws.Cell(5, 10).Value = "A";
        ws.Cell(5, 11).Value = "60"; ws.Cell(5, 12).Value = "66.67"; ws.Cell(5, 13).Value = "A*";
        ws.Cell(5, 14).Value = "74.5"; ws.Cell(5, 15).Value = "A";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Equal(2, rows.Count);

        // Student 1
        Assert.Equal("1", rows[0].StudentNumber);
        Assert.Equal(3, rows[0].Topics.Count);
        Assert.Equal("Biochemistry", rows[0].Topics[0].TopicName);
        Assert.Equal(17, rows[0].Topics[0].Mark);
        Assert.Equal("Biochem 2", rows[0].Topics[1].TopicName);
        Assert.Equal(22, rows[0].Topics[1].Mark);
        Assert.Equal("cells", rows[0].Topics[2].TopicName);
        Assert.Equal(12, rows[0].Topics[2].Mark);
        Assert.Equal(23, rows[0].Exam.Mark);
        Assert.Equal(25.55, rows[0].Exam.Percentage);
        Assert.Equal("E", rows[0].Exam.Grade);
        Assert.Equal(33.4, rows[0].AveragePercentage);
        Assert.Equal("E/D", rows[0].OverallGrade);

        // Student 2
        Assert.Equal("2", rows[1].StudentNumber);
        Assert.Equal(31, rows[1].Topics[0].Mark);
        Assert.Equal(41, rows[1].Topics[1].Mark);
        Assert.Equal(46, rows[1].Topics[2].Mark);
        Assert.Equal(60, rows[1].Exam.Mark);
        Assert.Equal(66.67, rows[1].Exam.Percentage);
        Assert.Equal("A*", rows[1].Exam.Grade);
        Assert.Equal(74.5, rows[1].AveragePercentage);
        Assert.Equal("A", rows[1].OverallGrade);
    }

    [Fact]
    public void Extract_DetectsUnnamedExamColumns_BetweenTopicsAndAv()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");

        ws.Cell(1, 2).Value = "Topic1";
        ws.Cell(2, 2).Value = "M"; ws.Cell(2, 3).Value = "%"; ws.Cell(2, 4).Value = "G";
        // Cols 5-7: no headers (unnamed exam)
        ws.Cell(1, 8).Value = "Av";
        ws.Cell(2, 8).Value = "%"; ws.Cell(2, 9).Value = "G";

        ws.Cell(3, 1).Value = "1";
        ws.Cell(3, 2).Value = "20"; ws.Cell(3, 3).Value = "50.0"; ws.Cell(3, 4).Value = "C";
        ws.Cell(3, 5).Value = "30"; ws.Cell(3, 6).Value = "75.0"; ws.Cell(3, 7).Value = "A";
        ws.Cell(3, 8).Value = "60.0"; ws.Cell(3, 9).Value = "B";

        var path = SaveToTempFile(wb);
        var rows = _extractor.Extract(path);

        Assert.Single(rows);
        Assert.Equal("Topic1", rows[0].Topics[0].TopicName);
        Assert.Equal(20, rows[0].Topics[0].Mark);
        Assert.Equal(30, rows[0].Exam.Mark);
        Assert.Equal(75.0, rows[0].Exam.Percentage);
        Assert.Equal("A", rows[0].Exam.Grade);
        Assert.Equal(60.0, rows[0].AveragePercentage);
        Assert.Equal("B", rows[0].OverallGrade);
    }
}
