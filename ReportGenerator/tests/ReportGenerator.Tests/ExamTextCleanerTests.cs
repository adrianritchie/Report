using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

public sealed class ExamTextCleanerTests
{
    // ── IsBoilerplate ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("P78874A01028")]
    [InlineData("P65759A0128")]
    [InlineData("P71663A0120")]
    [InlineData("  P78874A01028  ")]   // leading/trailing whitespace
    [InlineData("p78874a01028")]       // lowercase
    public void IsBoilerplate_ReturnsTrue_ForPaperCodes(string line)
        => Assert.True(ExamTextCleaner.IsBoilerplate(line));

    [Theory]
    [InlineData("DO NOT WRITE IN THIS AREA")]
    [InlineData("do not write in this area")]   // case-insensitive
    [InlineData("DO NOT WRITE IN THE SHADED AREA")]
    [InlineData("DO NOT WRITE OUTSIDE THE BOX")]
    [InlineData("TURN OVER")]
    [InlineData("Turn over")]
    public void IsBoilerplate_ReturnsTrue_ForBoilerphrases(string line)
        => Assert.True(ExamTextCleaner.IsBoilerplate(line));

    [Theory]
    [InlineData("Question 1")]
    [InlineData("Describe the water cycle.")]
    [InlineData("P78874A01028 extra text")]  // code not alone on line
    [InlineData("")]
    [InlineData("   ")]
    public void IsBoilerplate_ReturnsFalse_ForNormalText(string line)
        => Assert.False(ExamTextCleaner.IsBoilerplate(line));

    // ── Clean ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clean_RemovesPaperCodeLines()
    {
        var input = "Question 1\nP78874A01028\nDescribe the water cycle.";
        var result = ExamTextCleaner.Clean(input);
        Assert.DoesNotContain("P78874A01028", result);
        Assert.Contains("Question 1", result);
        Assert.Contains("Describe the water cycle.", result);
    }

    [Fact]
    public void Clean_RemovesDoNotWriteLines()
    {
        var input = "Question 2\nDO NOT WRITE IN THIS AREA\nExplain photosynthesis.";
        var result = ExamTextCleaner.Clean(input);
        Assert.DoesNotContain("DO NOT WRITE IN THIS AREA", result);
        Assert.Contains("Question 2", result);
        Assert.Contains("Explain photosynthesis.", result);
    }

    [Fact]
    public void Clean_CollapsesMultipleBlankLines()
    {
        var input = "Line A\n\n\n\nLine B";
        var result = ExamTextCleaner.Clean(input);
        // Should not contain two consecutive newlines after the single blank is kept
        Assert.DoesNotContain("\n\n\n", result);
        Assert.Contains("Line A", result);
        Assert.Contains("Line B", result);
    }

    [Fact]
    public void Clean_PreservesNormalContent()
    {
        var input = "Section 1\nAnswer all questions.\n\n1. What is osmosis?";
        var result = ExamTextCleaner.Clean(input);
        Assert.Equal(input.Trim(), result);
    }

    [Fact]
    public void Clean_ReturnsWhitespace_WhenInputIsWhitespace()
    {
        var result = ExamTextCleaner.Clean("   ");
        Assert.Equal("   ", result);
    }

    [Fact]
    public void Clean_RemovesMultipleBoilerplateTypes_InOnePaper()
    {
        var input = string.Join('\n',
            "P78874A01028",
            "Instructions to candidates",
            "DO NOT WRITE IN THIS AREA",
            "Answer ALL questions.",
            "TURN OVER",
            "Question 1");

        var result = ExamTextCleaner.Clean(input);

        Assert.DoesNotContain("P78874A01028", result);
        Assert.DoesNotContain("DO NOT WRITE IN THIS AREA", result);
        Assert.DoesNotContain("TURN OVER", result);
        Assert.Contains("Instructions to candidates", result);
        Assert.Contains("Answer ALL questions.", result);
        Assert.Contains("Question 1", result);
    }
}
