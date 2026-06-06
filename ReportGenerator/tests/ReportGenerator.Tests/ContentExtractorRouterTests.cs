using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

public sealed class ContentExtractorRouterTests
{
    private readonly ContentExtractorRouter _router = new();

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".txt")]
    [InlineData(".md")]
    [InlineData(".doc")]
    [InlineData(".rtf")]
    [InlineData(".docx")]
    public void Router_DoesNotThrowNotSupported_ForKnownExtensions(string extension)
    {
        // This merely verifies the router selects a handler without throwing NotSupportedException.
        // Actual file reading is tested via individual extractor tests.
        var path = $"fakefile{extension}";
        var ex = Record.ExceptionAsync(() => _router.ExtractAsync(path));
        Assert.NotNull(ex);
        Assert.IsNotType<NotSupportedException>(ex.Result?.InnerException ?? ex.Result);
    }

    [Fact]
    public async Task Router_ThrowsNotSupportedException_ForUnknownExtension()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => _router.ExtractAsync("document.xlsx"));
    }
}
