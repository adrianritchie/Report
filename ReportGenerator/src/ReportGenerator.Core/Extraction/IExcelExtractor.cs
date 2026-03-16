namespace ReportGenerator.Extraction;

public interface IExcelExtractor
{
    /// <summary>
    /// Reads an Excel (.xlsx) file and returns one <see cref="StudentRow"/> per
    /// non-empty data row. The first row is treated as column headings.
    /// Columns 1 and 2 must be last name and first name respectively;
    /// columns 3+ are feedback fields labelled by their heading.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the .xlsx file.</param>
    /// <returns>Ordered list of student rows (never null, may be empty).</returns>
    IReadOnlyList<StudentRow> Extract(string filePath);
}
