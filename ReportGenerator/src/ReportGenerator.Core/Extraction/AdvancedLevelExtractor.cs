using ClosedXML.Excel;

namespace ReportGenerator.Extraction;

public sealed class AdvancedLevelExtractor : IAdvancedLevelExtractor
{
    private const int StudentNumberColumn = 1;

    public IReadOnlyList<AdvancedLevelRow> Extract(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Advanced level spreadsheet not found: {filePath}", filePath);

        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        var subHeaderRow = DetectSubHeaderRow(sheet, lastColumn);
        if (subHeaderRow < 1)
            throw new InvalidOperationException(
                $"Advanced level spreadsheet has no recognizable header row (expected a row with 'M' sub-headers): " +
                $"'{Path.GetFileName(filePath)}'.");

        var topicRow = subHeaderRow - 1;
        var dataStartRow = subHeaderRow + 1;

        if (lastRow < dataStartRow)
            throw new InvalidOperationException(
                $"Advanced level spreadsheet has no student data rows (expected data from row {dataStartRow}): " +
                $"'{Path.GetFileName(filePath)}'.");

        var groups = ParseColumnStructure(sheet, lastColumn, topicRow, subHeaderRow, filePath);

        var rows = new List<AdvancedLevelRow>();
        for (var row = dataStartRow; row <= lastRow; row++)
        {
            var studentNumber = sheet.Cell(row, StudentNumberColumn).GetString().Trim();
            if (string.IsNullOrWhiteSpace(studentNumber))
                continue;

            var topics = groups.TopicCols
                .Select(g => new AdvancedLevelTopicMark(
                    g.Name,
                    TryParseInt(sheet.Cell(row, g.MCol).GetString()),
                    TryParseDouble(sheet.Cell(row, g.PctCol).GetString()),
                    NullIfEmpty(sheet.Cell(row, g.GCol).GetString())))
                .ToList();

            AdvancedLevelExamMark examMark;
            if (groups.ExamMCol.HasValue)
            {
                examMark = new AdvancedLevelExamMark(
                    TryParseInt(sheet.Cell(row, groups.ExamMCol.Value).GetString()),
                    TryParseDouble(sheet.Cell(row, groups.ExamPctCol!.Value).GetString()),
                    NullIfEmpty(sheet.Cell(row, groups.ExamGCol!.Value).GetString()));
            }
            else
            {
                examMark = new AdvancedLevelExamMark(null, null, null);
            }

            var avPct = groups.AvPctCol.HasValue
                ? TryParseDouble(sheet.Cell(row, groups.AvPctCol.Value).GetString())
                : null;

            var overallGrade = groups.OverallGradeCol.HasValue
                ? NullIfEmpty(sheet.Cell(row, groups.OverallGradeCol.Value).GetString())
                : null;

            rows.Add(new AdvancedLevelRow(studentNumber, topics, examMark, avPct, overallGrade));
        }

        return rows;
    }

    private static int DetectSubHeaderRow(IXLWorksheet sheet, int lastColumn)
    {
        var maxRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, 10);
        for (var row = 1; row <= maxRow; row++)
        {
            for (var col = StudentNumberColumn + 1; col <= lastColumn; col++)
            {
                if (string.Equals(sheet.Cell(row, col).GetString().Trim(), "M", StringComparison.OrdinalIgnoreCase))
                    return row;
            }
        }
        return 0;
    }

    private static (List<(int MCol, int PctCol, int GCol, string Name)> TopicCols,
        int? ExamMCol, int? ExamPctCol, int? ExamGCol,
        int? AvPctCol, int? OverallGradeCol)
        ParseColumnStructure(IXLWorksheet sheet, int lastColumn, int topicRow, int subHeaderRow, string filePath)
    {
        var topicCols = new List<(int MCol, int PctCol, int GCol, string Name)>();
        int? examMCol = null, examPctCol = null, examGCol = null;
        int? avPctCol = null, overallGradeCol = null;

        for (var col = StudentNumberColumn + 1; col <= lastColumn;)
        {
            var subHeader = sheet.Cell(subHeaderRow, col).GetString().Trim();

            if (string.Equals(subHeader, "M", StringComparison.OrdinalIgnoreCase))
            {
                var name = sheet.Cell(topicRow, col).GetString().Trim();
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Column{col}";

                if (string.Equals(name, "Exam", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Examination", StringComparison.OrdinalIgnoreCase))
                {
                    examMCol = col;
                    examPctCol = col + 1;
                    examGCol = col + 2;
                }
                else
                {
                    topicCols.Add((col, col + 1, col + 2, name));
                }

                col += 3;
            }
            else
            {
                var header = sheet.Cell(topicRow, col).GetString().Trim();
                var sub = sheet.Cell(subHeaderRow, col).GetString().Trim();

                if (header.Contains("AV", StringComparison.OrdinalIgnoreCase) ||
                    header.Contains("Average", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(sub, "G", StringComparison.OrdinalIgnoreCase))
                        overallGradeCol = col;
                    else
                        avPctCol = col;
                }
                else if (string.Equals(header, "G", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(header, "Grade", StringComparison.OrdinalIgnoreCase))
                {
                    overallGradeCol = col;
                }
                else if (string.IsNullOrWhiteSpace(header))
                {
                    if (string.Equals(sub, "G", StringComparison.OrdinalIgnoreCase))
                        overallGradeCol = col;
                    else if (string.Equals(sub, "%", StringComparison.OrdinalIgnoreCase))
                        avPctCol = col;
                }

                col++;
            }
        }

        if (!examMCol.HasValue)
        {
            var unnamedExamCol = DetectUnnamedExamColumns(sheet, topicRow, subHeaderRow, topicCols, avPctCol, overallGradeCol);
            if (unnamedExamCol.HasValue)
            {
                examMCol = unnamedExamCol;
                examPctCol = unnamedExamCol + 1;
                examGCol = unnamedExamCol + 2;
            }
        }

        return (topicCols, examMCol, examPctCol, examGCol, avPctCol, overallGradeCol);
    }

    private static int? DetectUnnamedExamColumns(
        IXLWorksheet sheet, int topicRow, int subHeaderRow,
        List<(int MCol, int PctCol, int GCol, string Name)> topicCols,
        int? avPctCol, int? overallGradeCol)
    {
        var assignedCols = new HashSet<int>();
        foreach (var t in topicCols) { assignedCols.Add(t.MCol); assignedCols.Add(t.PctCol); assignedCols.Add(t.GCol); }
        if (avPctCol.HasValue) assignedCols.Add(avPctCol.Value);
        if (overallGradeCol.HasValue) assignedCols.Add(overallGradeCol.Value);

        var lastTopicEnd = topicCols.Count > 0 ? topicCols[^1].GCol : StudentNumberColumn;
        var searchEnd = (avPctCol ?? overallGradeCol ?? sheet.LastColumnUsed()?.ColumnNumber() ?? 0) - 1;

        for (var col = lastTopicEnd + 1; col <= searchEnd;)
        {
            if (assignedCols.Contains(col)) { col++; continue; }

            var topicEmpty = string.IsNullOrWhiteSpace(sheet.Cell(topicRow, col).GetString().Trim());
            var subEmpty = string.IsNullOrWhiteSpace(sheet.Cell(subHeaderRow, col).GetString().Trim());

            if (topicEmpty && subEmpty &&
                col + 2 <= searchEnd &&
                !assignedCols.Contains(col + 1) && !assignedCols.Contains(col + 2))
            {
                var nextTopicEmpty = string.IsNullOrWhiteSpace(sheet.Cell(topicRow, col + 1).GetString().Trim());
                var nextSubEmpty = string.IsNullOrWhiteSpace(sheet.Cell(subHeaderRow, col + 1).GetString().Trim());
                var nextNextTopicEmpty = string.IsNullOrWhiteSpace(sheet.Cell(topicRow, col + 2).GetString().Trim());
                var nextNextSubEmpty = string.IsNullOrWhiteSpace(sheet.Cell(subHeaderRow, col + 2).GetString().Trim());

                if (nextTopicEmpty && nextSubEmpty && nextNextTopicEmpty && nextNextSubEmpty)
                    return col;
            }

            col++;
        }

        return null;
    }

    private static int? TryParseInt(string value)
    {
        var trimmed = value.Trim().TrimEnd('%');
        return int.TryParse(trimmed, out var result) ? result : null;
    }

    private static double? TryParseDouble(string value)
    {
        var trimmed = value.Trim().TrimEnd('%');
        if (!double.TryParse(trimmed, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            return null;

        return result;
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
