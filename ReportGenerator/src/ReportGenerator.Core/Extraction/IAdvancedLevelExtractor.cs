namespace ReportGenerator.Extraction;

public interface IAdvancedLevelExtractor
{
    IReadOnlyList<AdvancedLevelRow> Extract(string filePath);
}
