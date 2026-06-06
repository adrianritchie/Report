namespace ReportGenerator.Web.Models;

/// <summary>The visual state of a single step in the generation timeline.</summary>
public enum StepStatus
{
    Pending,
    InProgress,
    Done,
    Skipped,
    Failed
}

/// <summary>A single step in the generation timeline.</summary>
/// <param name="Label">Short description shown next to the icon.</param>
/// <param name="Status">Current visual state.</param>
/// <param name="Detail">Optional sub-text (e.g. retry warning).</param>
public sealed record ProgressStep(
    string Label,
    StepStatus Status = StepStatus.Pending,
    string? Detail = null);

/// <summary>A completed student report row shown in the results table.</summary>
public sealed record ReportRow(int SequenceNumber, string StudentName, string ReportText, string PromptText);

/// <summary>
/// A single example report entered by the teacher, labelled with a grade band.
/// Grade values: "H" (High), "VG" (Very Good), "G" (Good), "F" (Fair), "WT" (Working Towards).
/// </summary>
public sealed record ExampleReport(string Grade, string Text);
