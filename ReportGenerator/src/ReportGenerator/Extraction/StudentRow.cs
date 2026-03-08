namespace ReportGenerator.Extraction;

/// <summary>
/// Represents a single student row read from an Excel spreadsheet.
/// </summary>
/// <param name="LastName">Student's last name (column 1).</param>
/// <param name="FirstName">Student's first name (column 2).</param>
/// <param name="Fields">
/// Ordered list of (heading, value) pairs from columns 3 onwards.
/// A blank cell yields an empty string value; the heading is always included.
/// </param>
public sealed record StudentRow(
    string LastName,
    string FirstName,
    IReadOnlyList<(string Heading, string Value)> Fields)
{
    /// <summary>Full name in "Firstname Lastname" order.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
