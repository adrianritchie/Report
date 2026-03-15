using ClosedXML.Excel;
using ReportGenerator.Report;

namespace ReportGenerator.Tests;

public sealed class ReportWriterTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Opens the saved .xlsx and returns the first worksheet.</summary>
    private static IXLWorksheet OpenSheet(string path)
        => new XLWorkbook(path).Worksheets.First();

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_CreatesXlsxFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        try
        {
            using var writer = new ReportWriter();
            writer.AddRow(1, "Report text for student one.");

            var savedPath = await writer.SaveAsync(dir);

            Assert.True(File.Exists(savedPath));
            Assert.EndsWith(".xlsx", savedPath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_FileNameContainsReportsPrefix()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        try
        {
            using var writer = new ReportWriter();
            writer.AddRow(1, "Report body.");

            var savedPath = await writer.SaveAsync(dir);

            Assert.StartsWith("reports_", Path.GetFileName(savedPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_HeaderRowHasCorrectLabels()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        try
        {
            using var writer = new ReportWriter();
            var savedPath = await writer.SaveAsync(dir);

            var sheet = OpenSheet(savedPath);
            Assert.Equal("Student", sheet.Cell(1, 1).GetString());
            Assert.Equal("Report",  sheet.Cell(1, 2).GetString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AddRow_WritesSequenceNumberAndReportText()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        try
        {
            using var writer = new ReportWriter();
            writer.AddRow(7, "Excellent performance.");

            var savedPath = await writer.SaveAsync(dir);
            var sheet = OpenSheet(savedPath);

            Assert.Equal(7,                       sheet.Cell(2, 1).GetValue<int>());
            Assert.Equal("Excellent performance.", sheet.Cell(2, 2).GetString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task AddRow_MultipleStudentsWrittenInOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        try
        {
            using var writer = new ReportWriter();
            writer.AddRow(1, "Report for student 1.");
            writer.AddRow(2, "Report for student 2.");
            writer.AddRow(3, "Report for student 3.");

            var savedPath = await writer.SaveAsync(dir);
            var sheet = OpenSheet(savedPath);

            for (var i = 1; i <= 3; i++)
            {
                Assert.Equal(i,                          sheet.Cell(i + 1, 1).GetValue<int>());
                Assert.Equal($"Report for student {i}.", sheet.Cell(i + 1, 2).GetString());
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
