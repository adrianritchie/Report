namespace ReportGenerator.Extraction;

/// <summary>
/// Represents a single question (or sub-part) mark for a student.
/// </summary>
/// <param name="Label">Question label, e.g. "1a", "1b", "2".</param>
/// <param name="StudentMark">The student's mark, or <c>null</c> if the cell was blank (not attempted).</param>
/// <param name="MaxMark">The maximum available marks for this question/sub-part.</param>
public sealed record QuestionMark(string Label, int? StudentMark, int MaxMark);
