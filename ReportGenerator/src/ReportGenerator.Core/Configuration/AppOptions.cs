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
}
