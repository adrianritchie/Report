using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReportGenerator.Web.Models;

namespace ReportGenerator.Web.Services;

public sealed class WorkspaceStore
{
    private const int MaxVersions = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _rootPath;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public WorkspaceStore(IWebHostEnvironment env)
    {
        _rootPath = Path.Combine(env.ContentRootPath, "AppData", "workspaces");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<IReadOnlyList<WorkspaceSummary>> ListAsync(string? kind = null)
    {
        await _mutex.WaitAsync();
        try
        {
            var result = new List<WorkspaceSummary>();
            foreach (var dir in Directory.GetDirectories(_rootPath))
            {
                var path = Path.Combine(dir, "workspace.json");
                if (!File.Exists(path))
                    continue;

                var state = await ReadJsonAsync<WorkspaceState>(path);
                if (!string.IsNullOrWhiteSpace(kind)
                    && !string.Equals(state.Kind, kind, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new WorkspaceSummary
                {
                    Id = state.Id,
                    Name = state.Name,
                    Kind = state.Kind,
                    UpdatedUtc = state.UpdatedUtc,
                    CurrentVersion = state.CurrentVersion,
                });
            }

            return result
                .OrderByDescending(x => x.UpdatedUtc)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> CreateAsync(string name, string kind, string selectedModel, string prompt, string task)
    {
        await _mutex.WaitAsync();
        try
        {
            var trimmedName = string.IsNullOrWhiteSpace(name)
                ? $"Workspace {DateTimeOffset.Now:yyyy-MM-dd HHmm}"
                : name.Trim();

            EnsureUniqueName(trimmedName, kind, null);

            var id = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;
            var workspacePath = GetWorkspacePath(id);
            Directory.CreateDirectory(workspacePath);
            Directory.CreateDirectory(Path.Combine(workspacePath, "versions"));
            Directory.CreateDirectory(Path.Combine(workspacePath, "exam"));
            Directory.CreateDirectory(Path.Combine(workspacePath, "sheet"));
            Directory.CreateDirectory(Path.Combine(workspacePath, "reports"));

            var state = new WorkspaceState
            {
                Id = id,
                Name = trimmedName,
                Kind = kind,
                CreatedUtc = now,
                UpdatedUtc = now,
                SelectedModel = selectedModel,
                Prompt = prompt,
                Task = task,
            };

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "workspace_created");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> GetAsync(string workspaceId)
    {
        await _mutex.WaitAsync();
        try
        {
            return await LoadStateAsync(workspaceId);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> RenameAsync(string workspaceId, string newName)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var trimmed = newName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                throw new InvalidOperationException("Workspace name cannot be empty.");

            EnsureUniqueName(trimmed, state.Kind, workspaceId);
            state.Name = trimmed;
            state.UpdatedUtc = DateTimeOffset.UtcNow;
            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "workspace_renamed");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> CloneAsync(string sourceWorkspaceId, string newName)
    {
        await _mutex.WaitAsync();
        try
        {
            var source = await LoadStateAsync(sourceWorkspaceId);
            var trimmedName = string.IsNullOrWhiteSpace(newName)
                ? $"{source.Name} Copy"
                : newName.Trim();

            EnsureUniqueName(trimmedName, source.Kind, null);

            var now = DateTimeOffset.UtcNow;
            var clone = new WorkspaceState
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = trimmedName,
                Kind = source.Kind,
                CreatedUtc = now,
                UpdatedUtc = now,
                SelectedModel = source.SelectedModel,
                Prompt = source.Prompt,
                Task = source.Task,
                Examples = Clone(source.Examples),
            };

            var workspacePath = GetWorkspacePath(clone.Id);
            Directory.CreateDirectory(workspacePath);
            Directory.CreateDirectory(Path.Combine(workspacePath, "versions"));
            Directory.CreateDirectory(Path.Combine(workspacePath, "exam"));
            Directory.CreateDirectory(Path.Combine(workspacePath, "sheet"));
            Directory.CreateDirectory(Path.Combine(workspacePath, "reports"));

            await SaveStateAsync(clone);
            await CreateVersionInternalAsync(clone, "workspace_cloned");
            return clone;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task DeleteAsync(string workspaceId)
    {
        await _mutex.WaitAsync();
        try
        {
            var path = GetWorkspacePath(workspaceId);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> SaveConfigurationAsync(
        string workspaceId,
        string selectedModel,
        string prompt,
        string task,
        IReadOnlyList<ExampleReport> examples)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            state.SelectedModel = selectedModel;
            state.Prompt = prompt;
            state.Task = task;
            state.Examples = examples.ToList();
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "prompt_saved");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> SaveExamProcessingAsync(
        string workspaceId,
        string sourceFileName,
        byte[] sourceData,
        string rawText,
        string summaryGenerated,
        string summaryFinal,
        bool fallbackUsed)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var workspacePath = GetWorkspacePath(workspaceId);
            var examDir = Path.Combine(workspacePath, "exam");
            Directory.CreateDirectory(examDir);

            var extension = Path.GetExtension(sourceFileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".txt";

            var safeName = $"source{extension}";
            var sourcePath = Path.Combine(examDir, safeName);
            await File.WriteAllBytesAsync(sourcePath, sourceData);

            state.Exam.SourceFileName = sourceFileName;
            state.Exam.SourceHash = ComputeHash(sourceData);
            state.Exam.SourceFilePath = sourcePath;
            state.Exam.RawText = rawText;
            state.Exam.SummaryGenerated = summaryGenerated;
            state.Exam.SummaryFinal = summaryFinal;
            state.Exam.SummaryFallbackUsed = fallbackUsed;
            state.Exam.ProcessedUtc = DateTimeOffset.UtcNow;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "exam_processed");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> SaveSheetProcessingAsync(
        string workspaceId,
        string sourceFileName,
        byte[] sourceData,
        IReadOnlyList<WorkspaceStudentSnapshot> students)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var workspacePath = GetWorkspacePath(workspaceId);
            var sheetDir = Path.Combine(workspacePath, "sheet");
            Directory.CreateDirectory(sheetDir);

            var sourcePath = Path.Combine(sheetDir, "source.xlsx");
            await File.WriteAllBytesAsync(sourcePath, sourceData);

            state.Sheet.SourceFileName = sourceFileName;
            state.Sheet.SourceHash = ComputeHash(sourceData);
            state.Sheet.SourceFilePath = sourcePath;
            state.Sheet.Students = students.ToList();
            state.Sheet.ProcessedUtc = DateTimeOffset.UtcNow;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "sheet_preprocessed");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> SaveResultsSheetProcessingAsync(
        string workspaceId,
        string sourceFileName,
        byte[] sourceData,
        IReadOnlyList<WorkspaceResultsStudentSnapshot> students)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var workspacePath = GetWorkspacePath(workspaceId);
            var sheetDir = Path.Combine(workspacePath, "results-analysis");
            Directory.CreateDirectory(sheetDir);

            var sourcePath = Path.Combine(sheetDir, "source.xlsx");
            await File.WriteAllBytesAsync(sourcePath, sourceData);

            state.ResultsAnalysis.SourceFileName = sourceFileName;
            state.ResultsAnalysis.SourceHash = ComputeHash(sourceData);
            state.ResultsAnalysis.SourceFilePath = sourcePath;
            state.ResultsAnalysis.Students = students.ToList();
            state.ResultsAnalysis.ProcessedUtc = DateTimeOffset.UtcNow;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "results_sheet_preprocessed");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> UpdateSheetSnapshotAsync(
        string workspaceId,
        IReadOnlyList<WorkspaceStudentSnapshot> students)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            if (state.Sheet.Students.Count == 0)
                throw new InvalidOperationException("Preprocess the spreadsheet before editing it.");

            state.Sheet.Students = students.ToList();
            state.Sheet.ProcessedUtc = DateTimeOffset.UtcNow;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "sheet_snapshot_edited");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> UpdateResultsSheetSnapshotAsync(
        string workspaceId,
        IReadOnlyList<WorkspaceResultsStudentSnapshot> students)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            if (state.ResultsAnalysis.Students.Count == 0)
                throw new InvalidOperationException("Preprocess the results spreadsheet before editing it.");

            state.ResultsAnalysis.Students = students.ToList();
            state.ResultsAnalysis.ProcessedUtc = DateTimeOffset.UtcNow;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "results_sheet_snapshot_edited");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> UpdateExamSummaryAsync(string workspaceId, string summaryFinal)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            if (string.IsNullOrWhiteSpace(state.Exam.RawText))
                throw new InvalidOperationException("Process the exam paper before editing the summary.");

            state.Exam.SummaryFinal = summaryFinal;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "exam_summary_edited");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> SaveReportRunAsync(
        string workspaceId,
        IReadOnlyList<ReportRowSnapshot> rows,
        string reportFilePath)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var workspacePath = GetWorkspacePath(workspaceId);
            var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            var runDir = Path.Combine(workspacePath, "reports", runId);
            Directory.CreateDirectory(runDir);

            var destination = Path.Combine(runDir, "reports.xlsx");
            File.Copy(reportFilePath, destination, true);

            var run = new WorkspaceReportRunSnapshot
            {
                RunId = runId,
                CreatedUtc = DateTimeOffset.UtcNow,
                ReportFileName = "reports.xlsx",
                ReportFilePath = destination,
                Rows = rows.ToList(),
            };

            state.Reports.Runs.Insert(0, run);
            state.Reports.LatestRunId = runId;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "reports_generated");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> SaveResultsReportRunAsync(
        string workspaceId,
        IReadOnlyList<ReportRowSnapshot> rows,
        string reportFilePath)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var workspacePath = GetWorkspacePath(workspaceId);
            var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            var runDir = Path.Combine(workspacePath, "results-analysis", "runs", runId);
            Directory.CreateDirectory(runDir);

            var destination = Path.Combine(runDir, "reports.xlsx");
            File.Copy(reportFilePath, destination, true);

            var run = new WorkspaceReportRunSnapshot
            {
                RunId = runId,
                CreatedUtc = DateTimeOffset.UtcNow,
                ReportFileName = "reports.xlsx",
                ReportFilePath = destination,
                Rows = rows.ToList(),
            };

            state.ResultsAnalysis.Runs.Insert(0, run);
            state.ResultsAnalysis.LatestRunId = runId;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, "results_reports_generated");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceState> RestoreVersionAsync(string workspaceId, int versionNumber)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var entry = state.Versions.FirstOrDefault(v => v.Number == versionNumber)
                ?? throw new InvalidOperationException($"Version v{versionNumber} not found.");

            var snapshotPath = Path.Combine(GetWorkspacePath(workspaceId), "versions", entry.SnapshotFileName);
            var snapshot = await ReadJsonAsync<WorkspaceSnapshot>(snapshotPath);

            state.Name = snapshot.Name;
            state.Kind = snapshot.Kind;
            state.SelectedModel = snapshot.SelectedModel;
            state.Prompt = snapshot.Prompt;
            state.Task = snapshot.Task;
            state.Examples = snapshot.Examples;
            state.Exam = snapshot.Exam;
            state.Sheet = snapshot.Sheet;
            state.Reports = snapshot.Reports;
            state.ResultsAnalysis = snapshot.ResultsAnalysis;
            state.UpdatedUtc = DateTimeOffset.UtcNow;

            await SaveStateAsync(state);
            await CreateVersionInternalAsync(state, $"restored_from_v{versionNumber}");
            return state;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<WorkspaceVersionDiff> BuildDiffPreviewAsync(string workspaceId, int versionNumber)
    {
        await _mutex.WaitAsync();
        try
        {
            var state = await LoadStateAsync(workspaceId);
            var entry = state.Versions.FirstOrDefault(v => v.Number == versionNumber)
                ?? throw new InvalidOperationException($"Version v{versionNumber} not found.");

            var snapshotPath = Path.Combine(GetWorkspacePath(workspaceId), "versions", entry.SnapshotFileName);
            var snapshot = await ReadJsonAsync<WorkspaceSnapshot>(snapshotPath);

            var diff = new WorkspaceVersionDiff
            {
                VersionNumber = entry.Number,
                Reason = entry.Reason,
                CreatedUtc = entry.CreatedUtc,
                Sections =
                [
                    BuildConfigDiff(state, snapshot),
                    BuildExamDiff(state, snapshot),
                    BuildSheetDiff(state, snapshot),
                    BuildReportsDiff(state, snapshot),
                    BuildResultsAnalysisDiff(state, snapshot),
                ],
            };

            return diff;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private WorkspaceDiffSection BuildConfigDiff(WorkspaceState current, WorkspaceSnapshot previous)
    {
        var lines = new List<string>();
        if (!string.Equals(current.SelectedModel, previous.SelectedModel, StringComparison.Ordinal))
            lines.Add($"Model: '{previous.SelectedModel}' -> '{current.SelectedModel}'");

        lines.AddRange(BuildTextChanges("Prompt", previous.Prompt, current.Prompt));
        lines.AddRange(BuildTextChanges("Task", previous.Task, current.Task));

        if (previous.Examples.Count != current.Examples.Count)
            lines.Add($"Examples count: {previous.Examples.Count} -> {current.Examples.Count}");

        if (lines.Count == 0)
            lines.Add("No configuration changes.");

        return new WorkspaceDiffSection { Title = "Configuration", Lines = lines };
    }

    private WorkspaceDiffSection BuildExamDiff(WorkspaceState current, WorkspaceSnapshot previous)
    {
        var lines = new List<string>();
        if (!string.Equals(current.Exam.SourceHash, previous.Exam.SourceHash, StringComparison.Ordinal))
            lines.Add("Exam source file changed.");

        lines.AddRange(BuildTextChanges("Exam summary", previous.Exam.SummaryFinal, current.Exam.SummaryFinal));

        if (lines.Count == 0)
            lines.Add("No exam processing changes.");

        return new WorkspaceDiffSection { Title = "Exam", Lines = lines };
    }

    private WorkspaceDiffSection BuildSheetDiff(WorkspaceState current, WorkspaceSnapshot previous)
    {
        var lines = new List<string>();
        if (!string.Equals(current.Sheet.SourceHash, previous.Sheet.SourceHash, StringComparison.Ordinal))
            lines.Add("Spreadsheet file changed.");

        if (current.Sheet.Students.Count != previous.Sheet.Students.Count)
            lines.Add($"Students: {previous.Sheet.Students.Count} -> {current.Sheet.Students.Count}");

        if (lines.Count == 0)
            lines.Add("No spreadsheet preprocessing changes.");

        return new WorkspaceDiffSection { Title = "Spreadsheet", Lines = lines };
    }

    private WorkspaceDiffSection BuildReportsDiff(WorkspaceState current, WorkspaceSnapshot previous)
    {
        var lines = new List<string>();
        var currLatest = current.Reports.Runs.FirstOrDefault();
        var prevLatest = previous.Reports.Runs.FirstOrDefault();

        if (currLatest is null && prevLatest is null)
        {
            lines.Add("No report runs in either version.");
        }
        else if (currLatest is null)
        {
            lines.Add("Current workspace has no report runs.");
        }
        else if (prevLatest is null)
        {
            lines.Add("Current workspace has report runs; selected version had none.");
        }
        else if (currLatest.RunId != prevLatest.RunId)
        {
            lines.Add($"Latest run changed: {prevLatest.RunId} -> {currLatest.RunId}");
            lines.Add($"Rows: {prevLatest.Rows.Count} -> {currLatest.Rows.Count}");
        }
        else
        {
            lines.Add("Latest report run unchanged.");
        }

        return new WorkspaceDiffSection { Title = "Reports", Lines = lines };
    }

    private WorkspaceDiffSection BuildResultsAnalysisDiff(WorkspaceState current, WorkspaceSnapshot previous)
    {
        var lines = new List<string>();
        if (!string.Equals(current.ResultsAnalysis.SourceHash, previous.ResultsAnalysis.SourceHash, StringComparison.Ordinal))
            lines.Add("Results analysis spreadsheet changed.");

        if (current.ResultsAnalysis.Students.Count != previous.ResultsAnalysis.Students.Count)
            lines.Add($"Results analysis students: {previous.ResultsAnalysis.Students.Count} -> {current.ResultsAnalysis.Students.Count}");

        var currLatest = current.ResultsAnalysis.Runs.FirstOrDefault();
        var prevLatest = previous.ResultsAnalysis.Runs.FirstOrDefault();
        if (currLatest is null && prevLatest is null)
        {
            lines.Add("No results analysis report runs in either version.");
        }
        else if (currLatest is null)
        {
            lines.Add("Current workspace has no results analysis runs.");
        }
        else if (prevLatest is null)
        {
            lines.Add("Current workspace has results analysis runs; selected version had none.");
        }
        else if (!string.Equals(currLatest.RunId, prevLatest.RunId, StringComparison.Ordinal))
        {
            lines.Add($"Latest results analysis run changed: {prevLatest.RunId} -> {currLatest.RunId}");
            lines.Add($"Rows: {prevLatest.Rows.Count} -> {currLatest.Rows.Count}");
        }

        if (lines.Count == 0)
            lines.Add("No results analysis changes.");

        return new WorkspaceDiffSection { Title = "Results Reports", Lines = lines };
    }

    private static IEnumerable<string> BuildTextChanges(string title, string? previous, string? current)
    {
        var oldText = previous ?? string.Empty;
        var newText = current ?? string.Empty;
        if (string.Equals(oldText, newText, StringComparison.Ordinal))
            return [];

        var oldLines = oldText.Replace("\r", string.Empty).Split('\n').Length;
        var newLines = newText.Replace("\r", string.Empty).Split('\n').Length;
        return [$"{title} changed ({oldLines} lines -> {newLines} lines)."];
    }

    private async Task CreateVersionInternalAsync(WorkspaceState state, string reason)
    {
        var nextVersion = state.CurrentVersion + 1;
        var snapshot = new WorkspaceSnapshot
        {
            Name = state.Name,
            Kind = state.Kind,
            SelectedModel = state.SelectedModel,
            Prompt = state.Prompt,
            Task = state.Task,
            Examples = Clone(state.Examples),
            Exam = Clone(state.Exam),
            Sheet = Clone(state.Sheet),
            Reports = Clone(state.Reports),
            ResultsAnalysis = Clone(state.ResultsAnalysis),
        };

        var snapshotFile = $"v{nextVersion}.json";
        var snapshotPath = Path.Combine(GetWorkspacePath(state.Id), "versions", snapshotFile);
        await WriteJsonAsync(snapshotPath, snapshot);

        state.CurrentVersion = nextVersion;
        state.UpdatedUtc = DateTimeOffset.UtcNow;
        state.Versions.Insert(0, new WorkspaceVersionEntry
        {
            Number = nextVersion,
            CreatedUtc = DateTimeOffset.UtcNow,
            Reason = reason,
            SnapshotFileName = snapshotFile,
        });

        while (state.Versions.Count > MaxVersions)
        {
            var old = state.Versions[^1];
            var oldPath = Path.Combine(GetWorkspacePath(state.Id), "versions", old.SnapshotFileName);
            if (File.Exists(oldPath))
                File.Delete(oldPath);
            state.Versions.RemoveAt(state.Versions.Count - 1);
        }

        await SaveStateAsync(state);
    }

    private async Task<WorkspaceState> LoadStateAsync(string workspaceId)
    {
        var path = Path.Combine(GetWorkspacePath(workspaceId), "workspace.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("Workspace not found.");

        return await ReadJsonAsync<WorkspaceState>(path);
    }

    private Task SaveStateAsync(WorkspaceState state)
        => WriteJsonAsync(Path.Combine(GetWorkspacePath(state.Id), "workspace.json"), state);

    private static async Task<T> ReadJsonAsync<T>(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
        if (value is null)
            throw new InvalidOperationException($"Invalid JSON payload: {filePath}");

        return value;
    }

    private static async Task WriteJsonAsync<T>(string filePath, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
    }

    private void EnsureUniqueName(string name, string kind, string? ignoreWorkspaceId)
    {
        foreach (var dir in Directory.GetDirectories(_rootPath))
        {
            var path = Path.Combine(dir, "workspace.json");
            if (!File.Exists(path))
                continue;

            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<WorkspaceState>(json, JsonOptions);
            if (state is null)
                continue;

            if (string.Equals(state.Id, ignoreWorkspaceId, StringComparison.Ordinal))
                continue;

            if (!string.Equals(state.Kind, kind, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(state.Name, name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"A workspace named '{name}' already exists.");
        }
    }

    private string GetWorkspacePath(string workspaceId)
        => Path.Combine(_rootPath, workspaceId);

    private static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static T Clone<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to clone value.");
    }
}
