namespace ReportGenerator.Extraction;

public sealed record AdvancedLevelRow(
    string StudentNumber,
    IReadOnlyList<AdvancedLevelTopicMark> Topics,
    AdvancedLevelExamMark Exam,
    double? AveragePercentage,
    string? OverallGrade);

public sealed record AdvancedLevelTopicMark(
    string TopicName,
    int? Mark,
    double? Percentage,
    string? Grade);

public sealed record AdvancedLevelExamMark(
    int? Mark,
    double? Percentage,
    string? Grade);
