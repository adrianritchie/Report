namespace ReportGenerator.Report;

public sealed class PromptBuilder
{
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
            "  4. A recommended grade or mark with brief justification." + Environment.NewLine +
            "Use formal but accessible language appropriate for sharing with parents and students. " +
            "Do not invent facts not evidenced in the student's responses.");

        return sb.ToString();
    }
}
