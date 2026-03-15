namespace ReportGenerator.Extraction;

/// <summary>
/// Represents a single student row read from an Excel spreadsheet.
/// </summary>
/// <param name="Name">Student's full name (column 1).</param>
/// <param name="Fields">
/// Ordered list of (heading, value) pairs from columns 2 onwards.
/// A blank cell yields an empty string value; the heading is always included.
/// </param>
public sealed record StudentRow(
    string Name,
    IReadOnlyList<(string Heading, string Value)> Fields)
{
    /// <summary>Alias for <see cref="Name"/> — used where full name is expected.</summary>
    public string FullName => Name;
}
