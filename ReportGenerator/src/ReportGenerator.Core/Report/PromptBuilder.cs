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
    public string Build(
        string examText,
        string responsesText,
        string teacherPrompt,
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
        sb.AppendLine(
            "You are assisting a teacher. Using the exam paper and student responses above, " +
            "write a concise, professional teacher assessment report suitable for a school report card. " +
            "The report must include:" + Environment.NewLine +
            "  1. An overall performance summary (2-3 sentences)." + Environment.NewLine +
            "  2. Key strengths demonstrated by the student." + Environment.NewLine +
            "  3. Specific areas for improvement with actionable suggestions." + Environment.NewLine +
            "The report should not include:" + Environment.NewLine +
            "  1. A recommended grade or mark with brief justification." + Environment.NewLine +
            "Use formal but accessible language appropriate for sharing with parents and students. " +
            "Do not invent facts not evidenced in the student's responses." + Environment.NewLine +
            "Keep the report concise, ideally around 150 words." + Environment.NewLine +
            "Where possible relate feedback to specific parts of the exam paper and student responses, but avoid excessive detail.");

        return sb.ToString();
    }
}
