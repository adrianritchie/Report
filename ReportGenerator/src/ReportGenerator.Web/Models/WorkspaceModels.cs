namespace ReportGenerator.Web.Models;

public sealed class WorkspaceState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = WorkspaceKinds.Feedback;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public int CurrentVersion { get; set; }

    public string SelectedModel { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public List<ExampleReport> Examples { get; set; } = [];

    public WorkspaceExamState Exam { get; set; } = new();
    public WorkspaceSheetState Sheet { get; set; } = new();
    public WorkspaceReportsState Reports { get; set; } = new();
    public WorkspaceResultsAnalysisState ResultsAnalysis { get; set; } = new();
    public WorkspaceAdvancedLevelState AdvancedLevel { get; set; } = new();

    public List<WorkspaceVersionEntry> Versions { get; set; } = [];
}

public sealed class WorkspaceExamState
{
    public string? SourceFileName { get; set; }
    public string? SourceHash { get; set; }
    public string? SourceFilePath { get; set; }
    public string? RawText { get; set; }
    public string? SummaryGenerated { get; set; }
    public string? SummaryFinal { get; set; }
    public bool SummaryFallbackUsed { get; set; }
    public DateTimeOffset? ProcessedUtc { get; set; }
}

public sealed class WorkspaceSheetState
{
    public string? SourceFileName { get; set; }
    public string? SourceHash { get; set; }
    public string? SourceFilePath { get; set; }
    public List<WorkspaceStudentSnapshot> Students { get; set; } = [];
    public DateTimeOffset? ProcessedUtc { get; set; }
}

public sealed class WorkspaceReportsState
{
    public string? LatestRunId { get; set; }
    public List<WorkspaceReportRunSnapshot> Runs { get; set; } = [];
}

public sealed class WorkspaceResultsAnalysisState
{
    public string? SourceFileName { get; set; }
    public string? SourceHash { get; set; }
    public string? SourceFilePath { get; set; }
    public List<WorkspaceResultsStudentSnapshot> Students { get; set; } = [];
    public DateTimeOffset? ProcessedUtc { get; set; }
    public string? LatestRunId { get; set; }
    public List<WorkspaceReportRunSnapshot> Runs { get; set; } = [];
}

public sealed class WorkspaceResultsStudentSnapshot
{
    public string StudentNumber { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public string? ClassInteractionKeywords { get; set; }
    public List<WorkspaceQuestionMarkSnapshot> Marks { get; set; } = [];
    public int? Total { get; set; }
    public double? Percentage { get; set; }
    public int? Rank { get; set; }
    public string? Grade { get; set; }
}

public sealed class WorkspaceQuestionMarkSnapshot
{
    public string Label { get; set; } = string.Empty;
    public int? StudentMark { get; set; }
    public int MaxMark { get; set; }
}

public sealed class WorkspaceReportRunSnapshot
{
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public string? ReportFileName { get; set; }
    public string? ReportFilePath { get; set; }
    public List<ReportRowSnapshot> Rows { get; set; } = [];
}

public sealed class ReportRowSnapshot
{
    public int SequenceNumber { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string ReportText { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
}

public sealed class WorkspaceStudentSnapshot
{
    public string Name { get; set; } = string.Empty;
    public List<WorkspaceFieldSnapshot> Fields { get; set; } = [];
}

public sealed class WorkspaceFieldSnapshot
{
    public string Heading { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class WorkspaceVersionEntry
{
    public int Number { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SnapshotFileName { get; set; } = string.Empty;
}

public sealed class WorkspaceSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = WorkspaceKinds.Feedback;
    public DateTimeOffset UpdatedUtc { get; set; }
    public int CurrentVersion { get; set; }
}

public sealed class WorkspaceSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = WorkspaceKinds.Feedback;
    public string SelectedModel { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public List<ExampleReport> Examples { get; set; } = [];
    public WorkspaceExamState Exam { get; set; } = new();
    public WorkspaceSheetState Sheet { get; set; } = new();
    public WorkspaceReportsState Reports { get; set; } = new();
    public WorkspaceResultsAnalysisState ResultsAnalysis { get; set; } = new();
    public WorkspaceAdvancedLevelState AdvancedLevel { get; set; } = new();
}

public sealed class WorkspaceAdvancedLevelState
{
    public string? SourceFileName { get; set; }
    public string? SourceHash { get; set; }
    public string? SourceFilePath { get; set; }
    public List<WorkspaceAdvancedLevelStudentSnapshot> Students { get; set; } = [];
    public DateTimeOffset? ProcessedUtc { get; set; }
    public string? LatestRunId { get; set; }
    public List<WorkspaceReportRunSnapshot> Runs { get; set; } = [];
}

public sealed class WorkspaceAdvancedLevelStudentSnapshot
{
    public string StudentNumber { get; set; } = string.Empty;
    public List<WorkspaceAdvancedLevelTopicSnapshot> Topics { get; set; } = [];
    public WorkspaceAdvancedLevelExamSnapshot? Exam { get; set; }
    public double? AveragePercentage { get; set; }
    public string? OverallGrade { get; set; }
}

public sealed class WorkspaceAdvancedLevelTopicSnapshot
{
    public string TopicName { get; set; } = string.Empty;
    public int? Mark { get; set; }
    public double? Percentage { get; set; }
    public string? Grade { get; set; }
}

public sealed class WorkspaceAdvancedLevelExamSnapshot
{
    public int? Mark { get; set; }
    public double? Percentage { get; set; }
    public string? Grade { get; set; }
}

public static class WorkspaceKinds
{
    public const string Feedback = "feedback";
    public const string Results = "results";
    public const string AdvancedLevel = "a-level";
}

public sealed class WorkspaceVersionDiff
{
    public int VersionNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public List<WorkspaceDiffSection> Sections { get; set; } = [];
}

public sealed class WorkspaceDiffSection
{
    public string Title { get; set; } = string.Empty;
    public List<string> Lines { get; set; } = [];
}
