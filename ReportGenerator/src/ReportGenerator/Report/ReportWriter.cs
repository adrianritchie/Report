using ClosedXML.Excel;

namespace ReportGenerator.Report;

/// <summary>
/// Accumulates per-student report rows and saves them as a single .xlsx file.
/// Column A: Student (sequential number), Column B: Report (Ollama output).
/// </summary>
public sealed class ReportWriter : IDisposable
{
    private readonly XLWorkbook _workbook;
    private readonly IXLWorksheet _sheet;
    private int _nextRow = 2;

    public ReportWriter()
    {
        _workbook = new XLWorkbook();
        _sheet    = _workbook.AddWorksheet("Reports");

        // Header row
        _sheet.Cell(1, 1).Value = "Student";
        _sheet.Cell(1, 2).Value = "Report";

        var headerRow = _sheet.Row(1);
        headerRow.Style.Font.Bold = true;
    }

    /// <summary>
    /// Appends one student row to the workbook. Thread-unsafe; call sequentially.
    /// </summary>
    public void AddRow(int sequenceNumber, string reportText)
    {
        _sheet.Cell(_nextRow, 1).Value = sequenceNumber;
        _sheet.Cell(_nextRow, 2).Value = reportText.Trim();
        _sheet.Cell(_nextRow, 2).Style.Alignment.WrapText = true;
        _nextRow++;
    }

    /// <summary>
    /// Saves the workbook to <paramref name="outputDirectory"/> as
    /// <c>reports_&lt;timestamp&gt;.xlsx</c> and returns the full path.
    /// </summary>
    public Task<string> SaveAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        // Auto-fit column A; set a reasonable fixed width for the report column
        _sheet.Column(1).AdjustToContents();
        _sheet.Column(2).Width = 100;

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var filePath  = Path.Combine(outputDirectory, $"reports_{timestamp}.xlsx");

        _workbook.SaveAs(filePath);

        return Task.FromResult(filePath);
    }

    public void Dispose() => _workbook.Dispose();
}
