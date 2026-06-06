using ClosedXML.Excel;

namespace ReportGenerator.Extraction;

/// <summary>
/// Reads a student results spreadsheet (.xlsx) and returns one
/// <see cref="ResultsRow"/> per student data row.
///
/// Sheet layout:
///   Row 1  : Question numbers in cols 3..summary-start-1; cols 1-2 are student number/class.
///   Row 2  : Sub-part letters (may be blank — question has no sub-parts).
///   Row 3  : Maximum marks per question column.
///   Row 4+ : Student data — col 1 = student number, col 2 = class,
///             followed by summary columns identified by row-1 headers:
///             Total, Percentage (or %), Rank, Grade.
///             Any columns after Grade are ignored.
/// </summary>
public sealed class ResultsExtractor : IResultsExtractor
{
    private const int StudentNumberColumn = 1;
    private const int ClassColumn         = 2;
    private const int FirstQuestionColumn = 3;
    private const int HeaderRows          = 3;
    private const int HeaderLabelRow      = 1;

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

        var summaryColumns = FindSummaryColumns(sheet, lastColumn, filePath);
        var lastQuestionCol = summaryColumns.Total - 1;

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

            // Read summary columns.
            int? total = TryParseInt(sheet.Cell(row, summaryColumns.Total).GetString());
            double? percentage = TryParseDouble(sheet.Cell(row, summaryColumns.Percentage).GetString());
            int? rank = TryParseInt(sheet.Cell(row, summaryColumns.Rank).GetString());
            var grade = sheet.Cell(row, summaryColumns.Grade).GetString().Trim();
            if (string.IsNullOrWhiteSpace(grade))
                grade = null;

            rows.Add(new ResultsRow(studentNumber, studentClass, marks, total, percentage, rank, grade));
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

    private static SummaryColumns FindSummaryColumns(IXLWorksheet sheet, int lastColumn, string filePath)
    {
        int? totalColumn = null;
        int? percentageColumn = null;
        int? rankColumn = null;
        int? gradeColumn = null;

        for (var col = FirstQuestionColumn; col <= lastColumn; col++)
        {
            var header = sheet.Cell(HeaderLabelRow, col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(header))
                continue;

            if (totalColumn is null && IsTotalHeader(header))
            {
                totalColumn = col;
                continue;
            }

            if (percentageColumn is null && IsPercentageHeader(header))
            {
                percentageColumn = col;
                continue;
            }

            if (rankColumn is null && IsRankHeader(header))
            {
                rankColumn = col;
                continue;
            }

            if (gradeColumn is null && IsGradeHeader(header))
                gradeColumn = col;
        }

        if (totalColumn is null || percentageColumn is null || rankColumn is null || gradeColumn is null)
        {
            throw new InvalidOperationException(
                "Results spreadsheet is missing one or more required summary column headers " +
                "(Total, Percentage or %, Rank, Grade) in row 1: " +
                $"'{Path.GetFileName(filePath)}'.");
        }

        return new SummaryColumns(totalColumn.Value, percentageColumn.Value, rankColumn.Value, gradeColumn.Value);
    }

    private static bool IsTotalHeader(string value) =>
        string.Equals(value, "total", StringComparison.OrdinalIgnoreCase);

    private static bool IsPercentageHeader(string value) =>
        string.Equals(value, "percentage", StringComparison.OrdinalIgnoreCase) || value == "%";

    private static bool IsRankHeader(string value) =>
        string.Equals(value, "rank", StringComparison.OrdinalIgnoreCase);

    private static bool IsGradeHeader(string value) =>
        string.Equals(value, "grade", StringComparison.OrdinalIgnoreCase);

    private readonly record struct SummaryColumns(int Total, int Percentage, int Rank, int Grade);
}
