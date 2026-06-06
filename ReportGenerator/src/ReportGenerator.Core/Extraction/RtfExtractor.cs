using System.Text;
using System.Text.RegularExpressions;

namespace ReportGenerator.Extraction;

/// <summary>
/// Extracts plain text from RTF files (including files saved with a .doc extension
/// that are actually RTF, as commonly produced by exam paper tools such as Exampro).
///
/// RTF is a text-based markup format.  This extractor:
/// <list type="number">
///   <item>Detects whether the file is truly RTF (starts with <c>{\rtf</c>).</item>
///   <item>Strips embedded binary image blobs (<c>\pict</c> groups).</item>
///   <item>Strips RTF header groups (font table, colour table, stylesheet, info, fields, footer).</item>
///   <item>Converts paragraph/section breaks to newlines.</item>
///   <item>Decodes RTF hex-escaped characters (<c>\'xx</c>).</item>
///   <item>Removes all remaining RTF control words and braces.</item>
///   <item>Collapses whitespace and passes the result through <see cref="ExamTextCleaner"/>.</item>
/// </list>
/// </summary>
public sealed class RtfExtractor : IContentExtractor
{
    // Matches \pict groups including all nested braces and content (the image binary blob).
    // RTF images are wrapped in {\pict ... <hex data>}.
    private static readonly Regex PictGroupRegex = new(
        @"\{\\pict[^{}]*(?:\{[^{}]*\})*[^{}]*\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Matches header-level groups we want to discard entirely.
    private static readonly Regex HeaderGroupRegex = new(
        @"\{\\(?:fonttbl|colortbl|stylesheet|info|field|fldinst|fldrslt|footer|header)[^{}]*(?:\{[^{}]*\})*[^{}]*\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Matches RTF \'xx hex-encoded characters.
    // In regex, \\ matches a literal backslash; \' matches a literal apostrophe.
    private static readonly Regex HexEscapeRegex = new(
        @"\\'([0-9a-fA-F]{2})",
        RegexOptions.Compiled);

    // Matches RTF control words and symbols (backslash + letters + optional number + optional space).
    private static readonly Regex ControlWordRegex = new(
        @"\\([a-z*]+)(-?\d+)?\s?|\\([^a-z\r\n])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Long hex strings from image data that may remain after pict removal.
    private static readonly Regex HexBlobRegex = new(
        @"\b[0-9a-fA-F]{40,}\b",
        RegexOptions.Compiled);

    public async Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Exam file not found: {filePath}", filePath);

        var content = await File.ReadAllTextAsync(filePath, Encoding.ASCII, cancellationToken);

        if (!content.TrimStart().StartsWith(@"{\rtf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"File '{Path.GetFileName(filePath)}' does not appear to be an RTF document. " +
                "Files with a .doc or .rtf extension must contain RTF content.");

        var text = StripRtf(content);

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                $"No text could be extracted from '{Path.GetFileName(filePath)}'. " +
                "The file may be empty or contain only images.");

        return ExamTextCleaner.Clean(text);
    }

    // ── RTF stripping pipeline ────────────────────────────────────────────────

    public static string StripRtf(string rtf)
    {
        // 1. Remove embedded image blobs (\pict groups).
        //    These contain massive hex strings and nested braces.
        //    We use a simple brace-counting pass rather than regex for robustness.
        var text = RemoveRtfGroups(rtf, @"\pict");

        // 2. Remove other header groups we don't want.
        text = RemoveRtfGroups(text, @"\fonttbl");
        text = RemoveRtfGroups(text, @"\colortbl");
        text = RemoveRtfGroups(text, @"\stylesheet");
        text = RemoveRtfGroups(text, @"\info");
        text = RemoveRtfGroups(text, @"\footer");
        text = RemoveRtfGroups(text, @"\header");
        text = RemoveRtfGroups(text, @"\field");

        // 3. Convert paragraph/section/row breaks to newlines.
        text = Regex.Replace(text, @"\\par\b\s*", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\sect\b\s*", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\row\b\s*", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\cell\b\s*", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\line\b\s*", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\\tab\b\s*", "  ", RegexOptions.IgnoreCase);

        // 4. Decode \'xx hex escape sequences.
        //    RTF uses Windows-1252 encoding for ANSI documents, but since .NET Core
        //    requires explicit code page registration for non-ASCII encodings we use
        //    Latin-1 (ISO-8859-1) as a safe fallback — the two encodings are identical
        //    for the printable characters (0x20–0x7E) and most accented Latin characters
        //    found in UK exam papers (e.g. \'b0 → degree sign, \'25 → percent sign).
        text = HexEscapeRegex.Replace(text, m =>
        {
            var code = Convert.ToInt32(m.Groups[1].Value, 16);
            // Values 0x00–0x1F are control characters; skip them.
            if (code < 0x20) return string.Empty;
            // Use Latin-1 which maps the byte value directly to the Unicode code point.
            return ((char)code).ToString();
        });

        // 5. Remove any remaining long hex blobs (leftover image data).
        text = HexBlobRegex.Replace(text, string.Empty);

        // 6. Remove all remaining RTF control words.
        text = ControlWordRegex.Replace(text, string.Empty);

        // 7. Remove group braces.
        text = text.Replace("{", string.Empty).Replace("}", string.Empty);

        // 8. Normalise whitespace: collapse runs of spaces/tabs, then collapse blank lines.
        text = Regex.Replace(text, @"[^\S\r\n]+", " ");
        text = Regex.Replace(text, @"(\r?\n){3,}", "\n\n");

        return text.Trim();
    }

    /// <summary>
    /// Removes all RTF groups that start with the given control word by counting
    /// opening and closing braces.  This correctly handles nested braces inside
    /// the group (e.g. image data containing brace-like sequences).
    /// </summary>
    private static string RemoveRtfGroups(string rtf, string controlWord)
    {
        var result = new StringBuilder(rtf.Length);
        var i = 0;

        while (i < rtf.Length)
        {
            // Look for an opening brace followed (possibly with whitespace) by our control word.
            var braceIdx = rtf.IndexOf('{', i);
            if (braceIdx < 0)
            {
                result.Append(rtf, i, rtf.Length - i);
                break;
            }

            // Check whether this brace starts our target group.
            var afterBrace = braceIdx + 1;
            // Allow optional whitespace between { and the control word.
            while (afterBrace < rtf.Length && rtf[afterBrace] == ' ')
                afterBrace++;

            if (afterBrace + controlWord.Length <= rtf.Length &&
                string.Compare(rtf, afterBrace, controlWord, 0, controlWord.Length,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                // This is a group we want to skip. Count braces to find the end.
                result.Append(rtf, i, braceIdx - i);
                var depth = 1;
                var j = braceIdx + 1;
                while (j < rtf.Length && depth > 0)
                {
                    if (rtf[j] == '{') depth++;
                    else if (rtf[j] == '}') depth--;
                    j++;
                }
                i = j; // skip past the closing brace
            }
            else
            {
                // Not our target — copy up to and including this brace.
                result.Append(rtf, i, braceIdx - i + 1);
                i = braceIdx + 1;
            }
        }

        return result.ToString();
    }
}
