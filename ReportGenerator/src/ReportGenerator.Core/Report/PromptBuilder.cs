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

    /// <summary>
    /// Assembles the structured prompt sent to the Ollama Chat API.
    /// </summary>
    /// <param name="examText">Exam paper text (or LLM-generated summary).</param>
    /// <param name="responsesText">Student responses extracted from the spreadsheet.</param>
    /// <param name="teacherPrompt">Teacher instructions / additional guidance.</param>
    /// <param name="taskText">The [TASK] instruction that tells the LLM what to produce.</param>
    /// <param name="studentName">Optional student name prepended as a [STUDENT] section.</param>
    public string Build(
        string examText,
        string responsesText,
        string teacherPrompt,
        string taskText,
        string? studentName = null)
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

        sb.AppendLine("[TASK]");
        sb.AppendLine(taskText.Trim());

        return sb.ToString();
    }
}
