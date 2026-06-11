namespace ReportGenerator.Extraction;

/// <summary>
/// Represents a single student row extracted from a results spreadsheet.
/// </summary>
/// <param name="StudentNumber">The student's identifier number (column 1).</param>
/// <param name="Class">The student's class or group (column 2).</param>
/// <param name="Marks">Per-question marks in column order, covering all question columns (excluding summary columns).</param>
/// <param name="Total">Raw total marks, or <c>null</c> if the cell was blank.</param>
/// <param name="Percentage">Percentage score, or <c>null</c> if the cell was blank.</param>
/// <param name="Rank">Year-group rank, or <c>null</c> if the cell was blank.</param>
/// <param name="Grade">Grade text, or <c>null</c> if the cell was blank.</param>
/// <param name="ClassInteractionKeywords">
/// Optional teacher-provided keywords describing class interaction for this student.
/// </param>
public sealed record ResultsRow(
    string StudentNumber,
    string Class,
    IReadOnlyList<QuestionMark> Marks,
    int? Total,
    double? Percentage,
    int? Rank,
    string? Grade,
    string? ClassInteractionKeywords = null);
