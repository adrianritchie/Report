using ClosedXML.Excel;

namespace ReportGenerator.Extraction;

/// <summary>
/// Reads a teacher feedback spreadsheet (.xlsx) and returns one
/// <see cref="StudentRow"/> per non-empty data row.
///
/// Expected sheet layout:
///   Row 1        : Column headings  (col 1 = "Name",
///                                    col 2+ = feedback field labels)
///   Rows 2+      : One student per row; a row is skipped when the name
///                  cell is blank.
/// </summary>
public sealed class ExcelExtractor : IExcelExtractor
{
    private const int NameColumn       = 1;
    private const int FirstFieldColumn = 2;

    public IReadOnlyList<StudentRow> Extract(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Spreadsheet not found: {filePath}", filePath);

        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var lastRow    = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastColumn < 1)
            throw new InvalidOperationException(
                $"Spreadsheet must have at least 1 column (Name). " +
                $"Found {lastColumn} column(s) in '{Path.GetFileName(filePath)}'.");

        if (lastRow < 2)
            throw new InvalidOperationException(
                $"Spreadsheet has no data rows (only a heading row or is empty): '{Path.GetFileName(filePath)}'.");

        // Read headings from row 1 (columns 2+)
        var headings = new List<string>();
        for (var col = FirstFieldColumn; col <= lastColumn; col++)
            headings.Add(sheet.Cell(1, col).GetString().Trim());

        // Read student rows
        var rows = new List<StudentRow>();
        for (var row = 2; row <= lastRow; row++)
        {
            var name = sheet.Cell(row, NameColumn).GetString().Trim();

            // Skip rows where the name cell is blank
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var fields = new List<(string Heading, string Value)>();
            for (var col = FirstFieldColumn; col <= lastColumn; col++)
            {
                var headingIndex = col - FirstFieldColumn;
                var heading = headingIndex < headings.Count ? headings[headingIndex] : $"Column {col}";
                var value   = sheet.Cell(row, col).GetString().Trim();
                fields.Add((heading, value));
            }

            rows.Add(new StudentRow(name, fields));
        }

        return rows;
    }
}
