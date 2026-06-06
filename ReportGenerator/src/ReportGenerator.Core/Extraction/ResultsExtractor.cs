using ClosedXML.Excel;

namespace ReportGenerator.Extraction;

/// <summary>
/// Reads a student results spreadsheet (.xlsx) and returns one
/// <see cref="ResultsRow"/> per student data row.
///
/// Sheet layout (fixed):
///   Row 1  : Question numbers in cols 3..N-3; cols 1-2 and last 3 are ignored.
///   Row 2  : Sub-part letters (may be blank — question has no sub-parts).
///   Row 3  : Maximum marks per question column.
///   Row 4+ : Student data — col 1 = student number, col 2 = class,
///             cols 3..N-3 = marks, col N-2 = Total, col N-1 = Percentage, col N = Rank.
/// </summary>
public sealed class ResultsExtractor : IResultsExtractor
{
    private const int StudentNumberColumn = 1;
    private const int ClassColumn         = 2;
    private const int FirstQuestionColumn = 3;
    private const int HeaderRows          = 3;
    private const int TrailingColumns     = 3; // Total, Percentage, Rank — always the last 3

    public IReadOnlyList<ResultsRow> Extract(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Results spreadsheet not found: {filePath}", filePath);

        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var lastRow    = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (lastRow <= HeaderRows)
            throw new InvalidOperationException(
                $"Results spreadsheet has no student data rows (expected data from row {HeaderRows + 1}): " +
                $"'{Path.GetFileName(filePath)}'.");

        // The last 3 columns are always Total, Percentage, Rank.
        // Question columns are everything from FirstQuestionColumn up to lastColumn - TrailingColumns.
        var totalColumn      = lastColumn - 2;
        var percentageColumn = lastColumn - 1;
        var rankColumn       = lastColumn;
        var lastQuestionCol  = lastColumn - TrailingColumns;

        // Build column labels from the 3 header rows.
        // Label = question number (row 1) + sub-part letter (row 2, if non-blank).
        var columnLabels  = new Dictionary<int, string>();
        var columnMaxMark = new Dictionary<int, int>();

        for (var col = FirstQuestionColumn; col <= lastQuestionCol; col++)
        {
            var questionNumber = sheet.Cell(1, col).GetString().Trim();
            var subPart        = sheet.Cell(2, col).GetString().Trim();
            var maxMarkStr     = sheet.Cell(3, col).GetString().Trim();

            var label = string.IsNullOrWhiteSpace(subPart)
                ? questionNumber
                : $"{questionNumber}{subPart}";

            columnLabels[col] = string.IsNullOrWhiteSpace(label) ? $"Col{col}" : label;

            columnMaxMark[col] = int.TryParse(maxMarkStr, out var maxMark) ? maxMark : 0;
        }

        // Read student rows (row HeaderRows+1 onward).
        var rows = new List<ResultsRow>();

        for (var row = HeaderRows + 1; row <= lastRow; row++)
        {
            var studentNumber = sheet.Cell(row, StudentNumberColumn).GetString().Trim();

            // Skip rows where the student number cell is blank.
            if (string.IsNullOrWhiteSpace(studentNumber))
                continue;

            var studentClass = sheet.Cell(row, ClassColumn).GetString().Trim();

            // Read per-question marks.
            var marks = new List<QuestionMark>();
            for (var col = FirstQuestionColumn; col <= lastQuestionCol; col++)
            {
                var cellValue  = sheet.Cell(row, col).GetString().Trim();
                int? studentMark = int.TryParse(cellValue, out var parsed) ? parsed : null;

                marks.Add(new QuestionMark(columnLabels[col], studentMark, columnMaxMark[col]));
            }

            // Read the trailing summary columns.
            int? total = TryParseInt(sheet.Cell(row, totalColumn).GetString());
            double? percentage = TryParseDouble(sheet.Cell(row, percentageColumn).GetString());
            int? rank = TryParseInt(sheet.Cell(row, rankColumn).GetString());

            rows.Add(new ResultsRow(studentNumber, studentClass, marks, total, percentage, rank));
        }

        return rows;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static int? TryParseInt(string value)
    {
        var trimmed = value.Trim().TrimEnd('%');
        return int.TryParse(trimmed, out var result) ? result : null;
    }

    private static double? TryParseDouble(string value)
    {
        // Handle both "72.5" and "72.5%" formats.
        var trimmed = value.Trim().TrimEnd('%');
        if (!double.TryParse(trimmed, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            return null;

        // If the original string had a '%' suffix the value is already 0-100; return as-is.
        return result;
    }
}
