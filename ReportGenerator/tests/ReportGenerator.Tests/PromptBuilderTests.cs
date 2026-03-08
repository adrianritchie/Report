using ReportGenerator.Report;

namespace ReportGenerator.Tests;

public sealed class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    [Fact]
    public void Build_ContainsAllSections()
    {
        var result = _builder.Build(
            examText: "Q1: What is 2+2?",
            responsesText: "A1: 4",
            teacherPrompt: "Focus on accuracy.",
            studentName: "Alice Smith");

        Assert.Contains("[STUDENT]", result);
        Assert.Contains("Alice Smith", result);
        Assert.Contains("[EXAM PAPER]", result);
        Assert.Contains("Q1: What is 2+2?", result);
        Assert.Contains("[STUDENT RESPONSES]", result);
        Assert.Contains("A1: 4", result);
        Assert.Contains("[TEACHER INSTRUCTIONS]", result);
        Assert.Contains("Focus on accuracy.", result);
        Assert.Contains("[TASK]", result);
    }

    [Fact]
    public void Build_OmitsStudentSection_WhenNameIsNull()
    {
        var result = _builder.Build(
            examText: "Exam text",
            responsesText: "Response text",
            teacherPrompt: "Be concise.");

        Assert.DoesNotContain("[STUDENT]", result);
    }

    [Fact]
    public void Build_OmitsStudentSection_WhenNameIsWhitespace()
    {
        var result = _builder.Build(
            examText: "Exam text",
            responsesText: "Response text",
            teacherPrompt: "Be concise.",
            studentName: "   ");

        Assert.DoesNotContain("[STUDENT]", result);
    }

    [Fact]
    public void Build_TrimsInputText()
    {
        var result = _builder.Build(
            examText: "  Exam  ",
            responsesText: "  Response  ",
            teacherPrompt: "  Prompt  ");

        Assert.Contains("Exam", result);
        Assert.Contains("Response", result);
        Assert.Contains("Prompt", result);
    }

    [Fact]
    public void Build_AlwaysContainsTaskSection()
    {
        var result = _builder.Build("e", "r", "p");
        Assert.Contains("[TASK]", result);
        Assert.Contains("teacher assessment report", result);
    }
}
