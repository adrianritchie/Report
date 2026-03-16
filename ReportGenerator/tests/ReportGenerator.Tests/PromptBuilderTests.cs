using ReportGenerator.Report;

namespace ReportGenerator.Tests;

public sealed class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    // ── BuildExamSummaryPrompt ────────────────────────────────────────────────

    [Fact]
    public void BuildExamSummaryPrompt_ContainsExamText()
    {
        const string examText = "Question 1: Describe osmosis.";
        var result = _builder.BuildExamSummaryPrompt(examText);
        Assert.Contains(examText, result);
    }

    [Fact]
    public void BuildExamSummaryPrompt_ContainsExamPaperSection()
    {
        var result = _builder.BuildExamSummaryPrompt("Some exam content.");
        Assert.Contains("[EXAM PAPER]", result);
    }

    [Fact]
    public void BuildExamSummaryPrompt_ContainsTaskSection()
    {
        var result = _builder.BuildExamSummaryPrompt("Some exam content.");
        Assert.Contains("[TASK]", result);
    }

    [Fact]
    public void BuildExamSummaryPrompt_IsNotEmpty()
    {
        var result = _builder.BuildExamSummaryPrompt("Exam question here.");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildExamSummaryPrompt_ThrowsArgumentException_WhenExamTextIsEmpty(string input)
    {
        Assert.Throws<ArgumentException>(() => _builder.BuildExamSummaryPrompt(input));
    }

    // ── Build (student report prompt) ────────────────────────────────────────

    [Fact]
    public void Build_ContainsAllSections()
    {
        var result = _builder.Build("exam", "responses", "instructions", "Alice");
        Assert.Contains("[STUDENT]", result);
        Assert.Contains("[EXAM PAPER]", result);
        Assert.Contains("[STUDENT RESPONSES]", result);
        Assert.Contains("[TEACHER INSTRUCTIONS]", result);
        Assert.Contains("[TASK]", result);
    }

    [Fact]
    public void Build_OmitsStudentSection_WhenNameIsNull()
    {
        var result = _builder.Build("exam", "responses", "instructions", studentName: null);
        Assert.DoesNotContain("[STUDENT]", result);
    }

    [Fact]
    public void Build_OmitsStudentSection_WhenNameIsWhitespace()
    {
        var result = _builder.Build("exam", "responses", "instructions", studentName: "  ");
        Assert.DoesNotContain("[STUDENT]", result);
    }

    [Fact]
    public void Build_ContainsStudentName_WhenProvided()
    {
        var result = _builder.Build("exam", "responses", "instructions", "Bob Smith");
        Assert.Contains("Bob Smith", result);
    }

    [Fact]
    public void Build_ContainsExamAndResponsesText()
    {
        var result = _builder.Build("question about photosynthesis", "student said X", "write a report");
        Assert.Contains("question about photosynthesis", result);
        Assert.Contains("student said X", result);
        Assert.Contains("write a report", result);
    }
}
