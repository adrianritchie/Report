using System.Text;
using ReportGenerator.Extraction;

namespace ReportGenerator.Report;

public sealed class PromptBuilder
{
    /// <summary>
    /// Builds a prompt that asks the LLM to summarise an exam paper into a concise
    /// structured description of questions, topics, and assessment objectives.
    /// The result is intended to replace the raw exam text in subsequent student
    /// report prompts, reducing token usage and focusing the model's attention.
    /// </summary>
    public string BuildExamSummaryPrompt(string examText)
    {
        if (string.IsNullOrWhiteSpace(examText))
            throw new ArgumentException("Exam text must not be empty.", nameof(examText));

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("[EXAM PAPER]");
        sb.AppendLine(examText.Trim());
        sb.AppendLine();

        sb.AppendLine("[TASK]");
        sb.AppendLine(
            "You are given an exam paper above. Extract and summarise it in structured plain text." + Environment.NewLine +
            "Your summary must include:" + Environment.NewLine +
            "  1. The overall subject, topic, and any named source material." + Environment.NewLine +
            "  2. Each question — its number, what it asks, and the key skills or knowledge being assessed." + Environment.NewLine +
            "  3. Any marking guidance or assessment objectives stated in the paper." + Environment.NewLine +
            "Be concise. Do not reproduce instructions to candidates, rubric boilerplate, or page headers." + Environment.NewLine +
            "Output plain text only — no markdown, no bullet symbols, no headings with # characters.");

        return sb.ToString();
    }

    // Grade band display names, ordered from highest to lowest.
    private static readonly IReadOnlyList<(string Code, string Label)> GradeBands =
    [
        ("H",  "High"),
        ("VG", "Very Good"),
        ("G",  "Good"),
        ("F",  "Fair"),
        ("WT", "Working Towards"),
    ];

    /// <summary>
    /// Assembles the structured prompt sent to the Ollama Chat API.
    /// </summary>
    /// <param name="examText">Exam paper text (or LLM-generated summary).</param>
    /// <param name="responsesText">Student responses extracted from the spreadsheet.</param>
    /// <param name="teacherPrompt">Teacher instructions / additional guidance.</param>
    /// <param name="taskText">The [TASK] instruction that tells the LLM what to produce.</param>
    /// <param name="studentName">Optional student name prepended as a [STUDENT] section.</param>
    /// <param name="examples">
    /// Optional example reports to guide the model's tone and style.
    /// Each item is a <c>(grade, text)</c> tuple where grade is one of
    /// <c>"H"</c>, <c>"VG"</c>, <c>"G"</c>, <c>"F"</c>, or <c>"WT"</c>.
    /// When provided, an <c>[EXAMPLE REPORTS]</c> section is injected between
    /// <c>[TEACHER INSTRUCTIONS]</c> and <c>[TASK]</c>, ordered high → low.
    /// </param>
    public string Build(
        string examText,
        string responsesText,
        string teacherPrompt,
        string taskText,
        string? studentName = null,
        IReadOnlyList<(string Grade, string Text)>? examples = null)
    {
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(studentName))
        {
            sb.AppendLine($"[STUDENT]");
            sb.AppendLine(studentName.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("[EXAM PAPER]");
        sb.AppendLine(examText.Trim());
        sb.AppendLine();

        sb.AppendLine("[STUDENT RESPONSES]");
        sb.AppendLine(responsesText.Trim());
        sb.AppendLine();

        sb.AppendLine("[TEACHER INSTRUCTIONS]");
        sb.AppendLine(teacherPrompt.Trim());
        sb.AppendLine();

        if (examples is { Count: > 0 })
        {
            sb.AppendLine("[EXAMPLE REPORTS]");
            sb.AppendLine(
                "The following are real examples of reports written for this class, " +
                "labelled by grade band. Use them as a reference for the expected " +
                "tone, length, and phrasing.");
            sb.AppendLine();

            // Emit examples grouped in grade-band order (H → VG → G → F → WT).
            foreach (var (code, label) in GradeBands)
            {
                foreach (var ex in examples.Where(e =>
                    string.Equals(e.Grade, code, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.IsNullOrWhiteSpace(ex.Text))
                        continue;

                    sb.AppendLine($"--- {label} ---");
                    sb.AppendLine(ex.Text.Trim());
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("[TASK]");
        sb.AppendLine(taskText.Trim());

        return sb.ToString();
    }

    /// <summary>
    /// Assembles the structured prompt sent to the Ollama Chat API for a results-based report.
    /// The student placeholder name "Jane" is used in place of a real name, since the results
    /// spreadsheet contains only student numbers — the teacher can replace it after generation.
    /// </summary>
    /// <param name="examContext">Exam paper text or LLM-generated summary.</param>
    /// <param name="row">The student's results row from the spreadsheet.</param>
    /// <param name="teacherPrompt">Teacher instructions / additional guidance.</param>
    /// <param name="taskText">The [TASK] instruction that tells the LLM what to produce.</param>
    /// <param name="examples">
    /// Optional example reports to guide the model's tone and style.
    /// Each item is a <c>(grade, text)</c> tuple where grade is one of
    /// <c>"H"</c>, <c>"VG"</c>, <c>"G"</c>, <c>"F"</c>, or <c>"WT"</c>.
    /// When provided, an <c>[EXAMPLE REPORTS]</c> section is injected between
    /// <c>[TEACHER INSTRUCTIONS]</c> and <c>[TASK]</c>, ordered high → low.
    /// </param>
    public string BuildResultsPrompt(
        string examContext,
        ResultsRow row,
        string teacherPrompt,
        string taskText,
        IReadOnlyList<(string Grade, string Text)>? examples = null)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("[STUDENT]");
        sb.AppendLine($"Jane / {row.StudentNumber} / Class {row.Class}");
        sb.AppendLine();

        sb.AppendLine("[EXAM PAPER]");
        sb.AppendLine(examContext.Trim());
        sb.AppendLine();

        sb.AppendLine("[STUDENT RESULTS]");
        foreach (var mark in row.Marks)
        {
            if (mark.StudentMark is null)
                sb.AppendLine($"Q{mark.Label}: not attempted (max {mark.MaxMark})");
            else
                sb.AppendLine($"Q{mark.Label}: {mark.StudentMark} / {mark.MaxMark}");
        }

        if (row.Total is not null || row.Percentage is not null || row.Rank is not null)
        {
            var parts = new List<string>();
            if (row.Total      is not null) parts.Add($"Total: {row.Total}");
            if (row.Percentage is not null) parts.Add($"Percentage: {row.Percentage:0.##}%");
            if (row.Rank       is not null) parts.Add($"Rank in year: {row.Rank}");
            sb.AppendLine(string.Join(" | ", parts));
        }

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(row.ClassInteractionKeywords))
        {
            sb.AppendLine("[CLASS INTERACTION KEYWORDS]");
            sb.AppendLine(row.ClassInteractionKeywords.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("[TEACHER INSTRUCTIONS]");
        sb.AppendLine(teacherPrompt.Trim());
        sb.AppendLine();

        var gradeMatchedExamples = examples?
            .Where(e =>
                !string.IsNullOrWhiteSpace(row.Grade) &&
                string.Equals(e.Grade, row.Grade, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (gradeMatchedExamples is { Count: > 0 })
        {
            sb.AppendLine("[EXAMPLE REPORTS]");
            sb.AppendLine(
                "The following are real examples of reports written for this class, " +
                "for students in the same grade band. Use them as a reference for the expected " +
                "tone, length, and phrasing.");
            sb.AppendLine();

            foreach (var (code, label) in GradeBands)
            {
                foreach (var ex in gradeMatchedExamples.Where(e =>
                    string.Equals(e.Grade, code, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.IsNullOrWhiteSpace(ex.Text))
                        continue;

                    sb.AppendLine($"--- {label} ---");
                    sb.AppendLine(ex.Text.Trim());
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("[TASK]");
        sb.AppendLine(taskText.Trim());

        return sb.ToString();
    }

    /// <summary>
    /// Assembles the structured prompt for advanced-level reports, including both
    /// the student's results and their topic-level test results as separate sections.
    /// </summary>
    public string BuildAdvancedLevelPrompt(
        string examContext,
        ResultsRow resultsRow,
        AdvancedLevelRow? advancedRow,
        string teacherPrompt,
        string taskText,
        IReadOnlyList<(string Grade, string Text)>? examples = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[STUDENT]");
        sb.AppendLine($"Jane / {resultsRow.StudentNumber} / Class {resultsRow.Class}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(resultsRow.ClassInteractionKeywords))
        {
            sb.AppendLine("[CLASS INTERACTION KEYWORDS]");
            sb.AppendLine(resultsRow.ClassInteractionKeywords.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("[EXAM PAPER]");
        sb.AppendLine(examContext.Trim());
        sb.AppendLine();

        sb.AppendLine("[STUDENT RESULTS]");
        foreach (var mark in resultsRow.Marks)
        {
            if (mark.StudentMark is null)
                sb.AppendLine($"Q{mark.Label}: not attempted (max {mark.MaxMark})");
            else
                sb.AppendLine($"Q{mark.Label}: {mark.StudentMark} / {mark.MaxMark}");
        }

        if (resultsRow.Total is not null || resultsRow.Percentage is not null || resultsRow.Rank is not null)
        {
            var parts = new List<string>();
            if (resultsRow.Total is not null) parts.Add($"Total: {resultsRow.Total}");
            if (resultsRow.Percentage is not null) parts.Add($"Percentage: {resultsRow.Percentage:0.##}%");
            if (resultsRow.Rank is not null) parts.Add($"Rank in year: {resultsRow.Rank}");
            sb.AppendLine(string.Join(" | ", parts));
        }

        sb.AppendLine();

        if (advancedRow is not null)
        {
            sb.AppendLine("[TOPIC TEST RESULTS]");
            foreach (var topic in advancedRow.Topics)
            {
                var markStr = topic.Mark.HasValue ? topic.Mark.Value.ToString() : "—";
                var pctStr = topic.Percentage.HasValue ? $"{topic.Percentage:0.##}%" : "—";
                var gradeStr = topic.Grade ?? "—";
                sb.AppendLine($"{topic.TopicName}: {markStr} marks, {pctStr}, grade {gradeStr}");
            }

            if (advancedRow.Exam is not null)
            {
                var examMarkStr = advancedRow.Exam.Mark.HasValue ? advancedRow.Exam.Mark.Value.ToString() : "—";
                var examPctStr = advancedRow.Exam.Percentage.HasValue ? $"{advancedRow.Exam.Percentage:0.##}%" : "—";
                var examGradeStr = advancedRow.Exam.Grade ?? "—";
                sb.AppendLine($"Exam: {examMarkStr} marks, {examPctStr}, grade {examGradeStr}");
            }

            if (advancedRow.AveragePercentage.HasValue || !string.IsNullOrWhiteSpace(advancedRow.OverallGrade))
            {
                var avPctStr = advancedRow.AveragePercentage.HasValue ? $"{advancedRow.AveragePercentage:0.##}%" : "—";
                var gradeStr = advancedRow.OverallGrade ?? "—";
                sb.AppendLine($"Overall: {avPctStr} average, grade {gradeStr}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("[TEACHER INSTRUCTIONS]");
        sb.AppendLine(teacherPrompt.Trim());
        sb.AppendLine();

        var overallGrade = advancedRow?.OverallGrade ?? resultsRow.Grade;
        var matchedGrades = NormalizeAndExpandGrades(overallGrade);

        if (matchedGrades.Count > 0 && examples is { Count: > 0 })
        {
            var matchingExamples = examples
                .Where(e => !string.IsNullOrWhiteSpace(e.Text) &&
                    matchedGrades.Contains(e.Grade.Trim().ToUpperInvariant()))
                .ToList();

            if (matchingExamples.Count > 0)
            {
                sb.AppendLine("[EXAMPLE REPORTS]");
                sb.AppendLine(
                    "The following are real examples of reports written for this class " +
                    $"at grade {string.Join("/", matchedGrades)}. Use them as a reference for the expected " +
                    "tone, length, and phrasing.");
                sb.AppendLine();

                foreach (var ex in matchingExamples)
                {
                    sb.AppendLine($"--- Grade {ex.Grade} ---");
                    sb.AppendLine(ex.Text.Trim());
                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("[TASK]");
        sb.AppendLine(taskText.Trim());

        return sb.ToString();
    }

    private static IReadOnlyList<string> NormalizeAndExpandGrades(string? grade)
    {
        if (string.IsNullOrWhiteSpace(grade))
            return [];

        return grade.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(p =>
            {
                var normalized = p.Trim().ToUpperInvariant();
                return normalized switch
                {
                    "A*" or "A*" => (IEnumerable<string>)["A"],
                    "U" => ["E"],
                    _ => [normalized],
                };
            })
            .Distinct()
            .ToList();
    }
}
