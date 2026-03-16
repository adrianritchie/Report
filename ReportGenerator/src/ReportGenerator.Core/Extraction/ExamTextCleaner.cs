using System.Text.RegularExpressions;

namespace ReportGenerator.Extraction;

/// <summary>
/// Removes boilerplate text commonly found in Pearson/Edexcel exam papers:
/// <list type="bullet">
///   <item>Paper reference codes such as <c>P78874A01028</c> (P + digits + letter + digits).</item>
///   <item>Watermark phrases such as "DO NOT WRITE IN THIS AREA".</item>
/// </list>
/// Page-skipping (e.g. omitting the cover page) is handled by the caller because it is
/// format-specific (PDF pages vs. text blocks).
/// </summary>
public static class ExamTextCleaner
{
    // Matches Pearson paper reference codes: P followed by digits, a letter, then more digits.
    // Examples: P78874A01028, P65759A0128, P71663A0120
    private static readonly Regex PaperCodeRegex = new(
        @"^\s*P\d{4,6}[A-Z]\d{3,6}\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Boilerplate phrases that appear as standalone lines or as repeated watermark text.
    private static readonly string[] BoilerphraseFragments =
    [
        "DO NOT WRITE IN THIS AREA",
        "DO NOT WRITE IN THE SHADED AREA",
        "DO NOT WRITE OUTSIDE THE BOX",
        "TURN OVER",
        "Turn over",
    ];

    /// <summary>
    /// Removes boilerplate lines from <paramref name="text"/>.
    /// Each line is evaluated independently; lines that are a paper code or consist solely
    /// of a boilerplate phrase are dropped.  Blank lines produced by removal are collapsed.
    /// </summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var lines = text.Split('\n');
        var kept = new List<string>(lines.Length);
        var consecutiveBlanks = 0;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimEnd();

            if (IsBoilerplate(trimmed))
                continue;

            // Collapse runs of blank lines to a single blank line.
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                consecutiveBlanks++;
                if (consecutiveBlanks <= 1)
                    kept.Add(string.Empty);
            }
            else
            {
                consecutiveBlanks = 0;
                kept.Add(trimmed);
            }
        }

        return string.Join('\n', kept).Trim();
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="line"/> is a boilerplate line
    /// that should be removed from exam paper text.
    /// </summary>
    public static bool IsBoilerplate(string line)
    {
        var t = line.Trim();

        if (string.IsNullOrEmpty(t))
            return false;

        if (PaperCodeRegex.IsMatch(t))
            return true;

        foreach (var phrase in BoilerphraseFragments)
        {
            if (t.Equals(phrase, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
