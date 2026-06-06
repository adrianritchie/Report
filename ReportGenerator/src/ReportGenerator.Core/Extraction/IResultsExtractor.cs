namespace ReportGenerator.Extraction;

/// <summary>
/// Extracts student results from a structured results spreadsheet (.xlsx).
///
/// Expected sheet layout:
///   Row 1  : Column headings — cols 1-2 are blank (student number / class),
///             cols 3..N-3 are question numbers, last 3 cols are Total / Percentage / Rank.
///   Row 2  : Sub-part letters (e.g. a, b, c) — may be blank if a question has no sub-parts.
///   Row 3  : Maximum marks for each question column.
///   Row 4+ : One student per row.
/// </summary>
public interface IResultsExtractor
{
    /// <summary>
    /// Extracts all student rows from the spreadsheet at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Absolute path to the .xlsx file.</param>
    /// <returns>One <see cref="ResultsRow"/> per student row found in the sheet.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidOperationException">The sheet has no student data rows.</exception>
    IReadOnlyList<ResultsRow> Extract(string filePath);
}
