namespace ReportGenerator.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class ReportOptions
{
    public const string SectionName = "Report";

    public string DefaultOutputDirectory { get; set; } = ".";

    public string DefaultPrompt { get; set; } =
        "Provide a balanced, constructive assessment suitable for a school report card. " +
        "Highlight strengths, identify areas for improvement, and suggest a recommended grade.";

    public string DefaultTask { get; set; } =
        "You are assisting a teacher. Using the exam paper and student responses above, " +
        "write a concise, professional teacher assessment report suitable for a school report card. " +
        "The report must include:\n" +
        "  1. An overall performance summary (2-3 sentences).\n" +
        "  2. Key strengths demonstrated by the student.\n" +
        "  3. Specific areas for improvement with actionable suggestions.\n" +
        "The report should not include:\n" +
        "  1. A recommended grade or mark with brief justification.\n" +
        "Use formal but accessible language appropriate for sharing with parents and students. " +
        "Do not invent facts not evidenced in the student's responses.\n" +
        "Keep the report concise, ideally around 150 words.\n" +
        "Where possible relate feedback to specific parts of the exam paper and student responses, but avoid excessive detail.";
}
