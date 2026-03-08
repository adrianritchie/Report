using ClosedXML.Excel;

namespace ReportGenerator.Extraction;

/// <summary>
/// Reads a teacher feedback spreadsheet (.xlsx) and returns one
/// <see cref="StudentRow"/> per non-empty data row.
///
/// Expected sheet layout:
///   Row 1        : Column headings  (col 1 = "Last Name", col 2 = "First Name",
///                                    col 3+ = feedback field labels)
///   Rows 2+      : One student per row; a row is skipped when both name
///                  cells are blank.
/// </summary>
public sealed class ExcelExtractor : IExcelExtractor
{
    private const int LastNameColumn  = 1;
    private const int FirstNameColumn = 2;
    private const int FirstFieldColumn = 3;

    public IReadOnlyList<StudentRow> Extract(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Spreadsheet not found: {filePath}", filePath);

        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var lastRow    = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastColumn < 2)
            throw new InvalidOperationException(
                $"Spreadsheet must have at least 2 columns (Last Name, First Name). " +
                $"Found {lastColumn} column(s) in '{Path.GetFileName(filePath)}'.");

        if (lastRow < 2)
            throw new InvalidOperationException(
                $"Spreadsheet has no data rows (only a heading row or is empty): '{Path.GetFileName(filePath)}'.");

        // Read headings from row 1 (columns 3+)
        var headings = new List<string>();
        for (var col = FirstFieldColumn; col <= lastColumn; col++)
            headings.Add(sheet.Cell(1, col).GetString().Trim());

        // Read student rows
        var rows = new List<StudentRow>();
        for (var row = 2; row <= lastRow; row++)
        {
            var lastName  = sheet.Cell(row, LastNameColumn).GetString().Trim();
            var firstName = sheet.Cell(row, FirstNameColumn).GetString().Trim();

            // Skip rows where both name cells are blank
            if (string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(firstName))
                continue;

            var fields = new List<(string Heading, string Value)>();
            for (var col = FirstFieldColumn; col <= lastColumn; col++)
            {
                var headingIndex = col - FirstFieldColumn;
                var heading = headingIndex < headings.Count ? headings[headingIndex] : $"Column {col}";
                var value   = sheet.Cell(row, col).GetString().Trim();
                fields.Add((heading, value));
            }

            rows.Add(new StudentRow(lastName, firstName, fields));
        }

        return rows;
    }
}
