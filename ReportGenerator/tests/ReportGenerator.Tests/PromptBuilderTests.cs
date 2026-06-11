using ReportGenerator.Extraction;
using ReportGenerator.Report;

namespace ReportGenerator.Tests;

public sealed class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    private const string SampleTask = "Write a concise report.";

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
        var result = _builder.Build("exam", "responses", "instructions", SampleTask, "Alice");
        Assert.Contains("[STUDENT]", result);
        Assert.Contains("[EXAM PAPER]", result);
        Assert.Contains("[STUDENT RESPONSES]", result);
        Assert.Contains("[TEACHER INSTRUCTIONS]", result);
        Assert.Contains("[TASK]", result);
    }

    [Fact]
    public void Build_ContainsProvidedTaskText()
    {
        const string task = "Produce a haiku summarising the student's performance.";
        var result = _builder.Build("exam", "responses", "instructions", task);
        Assert.Contains(task, result);
    }

    [Fact]
    public void Build_OmitsStudentSection_WhenNameIsNull()
    {
        var result = _builder.Build("exam", "responses", "instructions", SampleTask, studentName: null);
        Assert.DoesNotContain("[STUDENT]", result);
    }

    [Fact]
    public void Build_OmitsStudentSection_WhenNameIsWhitespace()
    {
        var result = _builder.Build("exam", "responses", "instructions", SampleTask, studentName: "  ");
        Assert.DoesNotContain("[STUDENT]", result);
    }

    [Fact]
    public void Build_ContainsStudentName_WhenProvided()
    {
        var result = _builder.Build("exam", "responses", "instructions", SampleTask, "Bob Smith");
        Assert.Contains("Bob Smith", result);
    }

    [Fact]
    public void Build_ContainsExamAndResponsesText()
    {
        var result = _builder.Build("question about photosynthesis", "student said X", "write a report", SampleTask);
        Assert.Contains("question about photosynthesis", result);
        Assert.Contains("student said X", result);
        Assert.Contains("write a report", result);
    }

    // ── Build — [EXAMPLE REPORTS] section ────────────────────────────────────

    [Fact]
    public void Build_ContainsExampleReportsSection_WhenExamplesProvided()
    {
        var examples = new[] { ("H", "An outstanding performance overall.") };
        var result = _builder.Build("exam", "responses", "instructions", SampleTask,
            examples: examples);
        Assert.Contains("[EXAMPLE REPORTS]", result);
    }

    [Fact]
    public void Build_ExampleTextAppearsInOutput()
    {
        const string exampleText = "This student demonstrated excellent understanding.";
        var examples = new[] { ("VG", exampleText) };
        var result = _builder.Build("exam", "responses", "instructions", SampleTask,
            examples: examples);
        Assert.Contains(exampleText, result);
    }

    [Fact]
    public void Build_ExamplesAreOrderedByGradeBand()
    {
        // Provide examples in reverse grade order; expect H before WT in output.
        var examples = new[]
        {
            ("WT", "Working towards target."),
            ("H",  "Exceptional work."),
        };
        var result = _builder.Build("exam", "responses", "instructions", SampleTask,
            examples: examples);
        var indexH  = result.IndexOf("Exceptional work.", StringComparison.Ordinal);
        var indexWT = result.IndexOf("Working towards target.", StringComparison.Ordinal);
        Assert.True(indexH < indexWT, "High example should appear before Working Towards example.");
    }

    [Fact]
    public void Build_OmitsExampleSection_WhenExamplesIsNull()
    {
        var result = _builder.Build("exam", "responses", "instructions", SampleTask,
            examples: null);
        Assert.DoesNotContain("[EXAMPLE REPORTS]", result);
    }

    [Fact]
    public void Build_OmitsExampleSection_WhenExamplesIsEmpty()
    {
        var result = _builder.Build("exam", "responses", "instructions", SampleTask,
            examples: []);
        Assert.DoesNotContain("[EXAMPLE REPORTS]", result);
    }
}

// ── BuildResultsPrompt ────────────────────────────────────────────────────────

public sealed class BuildResultsPromptTests
{
    private readonly PromptBuilder _builder = new();

    private const string SampleTask = "Write a concise results report.";

    private static ResultsRow MakeRow(
        string studentNumber = "12345",
        string studentClass  = "10A",
        int?   total         = 85,
        double? percentage   = 72.5,
        int?   rank          = 4,
        string? grade        = null,
        string? classInteractionKeywords = null,
        IReadOnlyList<QuestionMark>? marks = null)
    {
        marks ??= new[]
        {
            new QuestionMark("1a", 2, 3),
            new QuestionMark("1b", 4, 4),
            new QuestionMark("2",  null, 5),  // not attempted
        };

        return new ResultsRow(studentNumber, studentClass, marks, total, percentage, rank, grade, classInteractionKeywords);
    }

    [Fact]
    public void BuildResultsPrompt_IncludesStudentSection()
    {
        var row = MakeRow();
        var result = _builder.BuildResultsPrompt("exam context", row, "instructions", SampleTask);

        Assert.Contains("[STUDENT]", result);
        Assert.Contains("Jane",    result);
        Assert.Contains("12345",   result);
        Assert.Contains("10A",     result);
    }

    [Fact]
    public void BuildResultsPrompt_IncludesExamPaperSection()
    {
        var row = MakeRow();
        var result = _builder.BuildResultsPrompt("photosynthesis exam content", row, "instructions", SampleTask);

        Assert.Contains("[EXAM PAPER]", result);
        Assert.Contains("photosynthesis exam content", result);
    }

    [Fact]
    public void BuildResultsPrompt_IncludesStudentResultsSection()
    {
        var row = MakeRow();
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask);

        Assert.Contains("[STUDENT RESULTS]", result);
    }

    [Fact]
    public void BuildResultsPrompt_FormatsAttemptedMarksCorrectly()
    {
        var row = MakeRow();
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask);

        // Q1a: 2 / 3 and Q1b: 4 / 4
        Assert.Contains("Q1a: 2 / 3", result);
        Assert.Contains("Q1b: 4 / 4", result);
    }

    [Fact]
    public void BuildResultsPrompt_ShowsNotAttempted_ForNullMark()
    {
        var row = MakeRow();
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask);

        // Q2 has a null StudentMark
        Assert.Contains("Q2: not attempted", result);
    }

    [Fact]
    public void BuildResultsPrompt_IncludesTotalLine_WhenPresent()
    {
        var row = MakeRow(total: 85, percentage: 72.5, rank: 4);
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask);

        Assert.Contains("Total: 85",      result);
        Assert.Contains("Percentage: 72.5%", result);
        Assert.Contains("Rank in year: 4", result);
    }

    [Fact]
    public void BuildResultsPrompt_OmitsTotalLine_WhenAllNull()
    {
        var row = MakeRow(total: null, percentage: null, rank: null);
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask);

        Assert.DoesNotContain("Total:",        result);
        Assert.DoesNotContain("Percentage:",   result);
        Assert.DoesNotContain("Rank in year:", result);
    }

    [Fact]
    public void BuildResultsPrompt_IncludesTeacherInstructionsAndTask()
    {
        var row = MakeRow();
        var result = _builder.BuildResultsPrompt("exam", row, "focus on algebra", SampleTask);

        Assert.Contains("[TEACHER INSTRUCTIONS]", result);
        Assert.Contains("focus on algebra",       result);
        Assert.Contains("[TASK]",                 result);
        Assert.Contains(SampleTask,               result);
    }

    [Fact]
    public void BuildResultsPrompt_IncludesClassInteractionKeywords_WhenProvided()
    {
        var row = MakeRow(classInteractionKeywords: "positive contributions, helps peers");
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask);

        Assert.Contains("[CLASS INTERACTION KEYWORDS]", result);
        Assert.Contains("positive contributions, helps peers", result);
    }

    [Fact]
    public void BuildResultsPrompt_ContainsExampleReportsSection_WhenExamplesProvided()
    {
        var row = MakeRow(grade: "G");
        var examples = new[] { ("G", "A solid effort across the paper.") };
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask,
            examples: examples);
        Assert.Contains("[EXAMPLE REPORTS]", result);
        Assert.Contains("A solid effort across the paper.", result);
    }

    [Fact]
    public void BuildResultsPrompt_OmitsExampleSection_WhenNoGradeMatchedExamples()
    {
        var row = MakeRow(grade: "VG");
        var examples = new[] { ("G", "A solid effort across the paper.") };
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask,
            examples: examples);
        Assert.DoesNotContain("[EXAMPLE REPORTS]", result);
        Assert.DoesNotContain("A solid effort across the paper.", result);
    }

    [Fact]
    public void BuildResultsPrompt_OmitsExampleSection_WhenExamplesIsNull()
    {
        var row = MakeRow();
        var result = _builder.BuildResultsPrompt("exam", row, "instructions", SampleTask,
            examples: null);
        Assert.DoesNotContain("[EXAMPLE REPORTS]", result);
    }
}
