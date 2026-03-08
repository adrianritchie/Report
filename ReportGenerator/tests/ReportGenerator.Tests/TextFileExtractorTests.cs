using ReportGenerator.Extraction;

namespace ReportGenerator.Tests;

public sealed class TextFileExtractorTests
{
    private readonly TextFileExtractor _extractor = new();

    [Fact]
    public async Task ExtractAsync_ReturnsContent_WhenFileExists()
    {
        // Arrange
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "  Hello world  ");

        try
        {
            // Act
            var result = await _extractor.ExtractAsync(path);

            // Assert
            Assert.Equal("Hello world", result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAsync_ThrowsFileNotFoundException_WhenFileMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _extractor.ExtractAsync("/nonexistent/path/file.txt"));
    }

    [Fact]
    public async Task ExtractAsync_ThrowsInvalidOperationException_WhenFileIsEmpty()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "   ");

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _extractor.ExtractAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
