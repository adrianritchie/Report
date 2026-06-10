using ReportGenerator.Configuration;
using ReportGenerator.Extraction;
using ReportGenerator.Ollama;
using ReportGenerator.Report;
using ReportGenerator.Web.Models;

namespace ReportGenerator.Web.Services;

public sealed class WorkspacePipelineService(
    WorkspaceStore workspaceStore,
    IContentExtractor contentExtractor,
    IExcelExtractor excelExtractor,
    IResultsExtractor resultsExtractor,
    PromptBuilder promptBuilder,
    IOllamaClient ollamaClient,
    OllamaOptions ollamaOptions,
    ReportOptions reportOptions)
{
    public Task<WorkspaceState> SaveConfigurationAsync(
        string workspaceId,
        string selectedModel,
        string prompt,
        string task,
        IReadOnlyList<ExampleReport> examples)
        => workspaceStore.SaveConfigurationAsync(workspaceId, selectedModel, prompt, task, examples);

    public Task<WorkspaceState> SaveExamSummaryAsync(string workspaceId, string summaryFinal)
        => workspaceStore.UpdateExamSummaryAsync(workspaceId, summaryFinal);

    public Task<WorkspaceState> SaveSheetSnapshotAsync(
        string workspaceId,
        IReadOnlyList<WorkspaceStudentSnapshot> students)
        => workspaceStore.UpdateSheetSnapshotAsync(workspaceId, students);

    public Task<WorkspaceState> SaveResultsSheetSnapshotAsync(
        string workspaceId,
        IReadOnlyList<WorkspaceResultsStudentSnapshot> students)
        => workspaceStore.UpdateResultsSheetSnapshotAsync(workspaceId, students);

    public async Task<WorkspaceState> ProcessExamAsync(
        string workspaceId,
        string fileName,
        byte[] fileData,
        string? overrideSummary,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"reportgen_exam_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".txt";

            var examPath = Path.Combine(tempDir, $"exam{extension}");
            await File.WriteAllBytesAsync(examPath, fileData, cancellationToken);

            var examText = await contentExtractor.ExtractAsync(examPath, cancellationToken);

            var workspace = await workspaceStore.GetAsync(workspaceId);
            ollamaOptions.Model = workspace.SelectedModel;

            var summaryPrompt = promptBuilder.BuildExamSummaryPrompt(examText);

            string summaryGenerated;
            var fallbackUsed = false;
            try
            {
                summaryGenerated = await ollamaClient.SendPromptAsync(summaryPrompt, cancellationToken);
            }
            catch
            {
                summaryGenerated = examText;
                fallbackUsed = true;
            }

            var summaryFinal = string.IsNullOrWhiteSpace(overrideSummary)
                ? summaryGenerated
                : overrideSummary;

            return await workspaceStore.SaveExamProcessingAsync(
                workspaceId,
                fileName,
                fileData,
                examText,
                summaryGenerated,
                summaryFinal,
                fallbackUsed);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    public async Task<WorkspaceState> PreprocessSheetAsync(
        string workspaceId,
        string fileName,
        byte[] fileData,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"reportgen_sheet_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sheetPath = Path.Combine(tempDir, "sheet.xlsx");
            await File.WriteAllBytesAsync(sheetPath, fileData, cancellationToken);
            var rows = excelExtractor.Extract(sheetPath);

            var students = rows
                .Select(r => new WorkspaceStudentSnapshot
                {
                    Name = r.FullName,
                    Fields = r.Fields
                        .Select(f => new WorkspaceFieldSnapshot
                        {
                            Heading = f.Heading,
                            Value = f.Value,
                        })
                        .ToList(),
                })
                .ToList();

            return await workspaceStore.SaveSheetProcessingAsync(workspaceId, fileName, fileData, students);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    public async Task<WorkspaceState> PreprocessResultsSheetAsync(
        string workspaceId,
        string fileName,
        byte[] fileData,
        CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"reportgen_results_sheet_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sheetPath = Path.Combine(tempDir, "sheet.xlsx");
            await File.WriteAllBytesAsync(sheetPath, fileData, cancellationToken);
            var rows = resultsExtractor.Extract(sheetPath);

            var students = rows
                .Select(r => new WorkspaceResultsStudentSnapshot
                {
                    StudentNumber = r.StudentNumber,
                    Class = r.Class,
                    Marks = r.Marks
                        .Select(m => new WorkspaceQuestionMarkSnapshot
                        {
                            Label = m.Label,
                            StudentMark = m.StudentMark,
                            MaxMark = m.MaxMark,
                        })
                        .ToList(),
                    Total = r.Total,
                    Percentage = r.Percentage,
                    Rank = r.Rank,
                    Grade = r.Grade,
                })
                .ToList();

            return await workspaceStore.SaveResultsSheetProcessingAsync(workspaceId, fileName, fileData, students);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    public async Task<(WorkspaceState Workspace, IReadOnlyList<ProgressStep> Steps)> GenerateReportsAsync(
        string workspaceId,
        Action<ProgressStep>? onStudentStep = null,
        Action<ReportRowSnapshot>? onReportGenerated = null,
        CancellationToken cancellationToken = default)
    {
        var workspace = await workspaceStore.GetAsync(workspaceId);
        if (string.IsNullOrWhiteSpace(workspace.Exam.SummaryFinal))
            throw new InvalidOperationException("Process the exam first.");
        if (workspace.Sheet.Students.Count == 0)
            throw new InvalidOperationException("Preprocess the spreadsheet first.");

        var effectivePrompt = string.IsNullOrWhiteSpace(workspace.Prompt)
            ? reportOptions.DefaultPrompt
            : workspace.Prompt;
        var effectiveTask = string.IsNullOrWhiteSpace(workspace.Task)
            ? reportOptions.DefaultTask
            : workspace.Task;

        var tempDir = Path.Combine(Path.GetTempPath(), $"reportgen_run_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var steps = new List<ProgressStep>();
        var rows = new List<ReportRowSnapshot>();

        try
        {
            ollamaOptions.Model = workspace.SelectedModel;
            using var writer = new ReportWriter();

            for (var i = 0; i < workspace.Sheet.Students.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var student = workspace.Sheet.Students[i];
                var responsesText = string.Join(Environment.NewLine,
                    student.Fields.Select(f => $"{f.Heading}: {f.Value}"));

                var fullPrompt = promptBuilder.Build(
                    workspace.Exam.SummaryFinal!,
                    responsesText,
                    effectivePrompt,
                    effectiveTask,
                    student.Name,
                    workspace.Examples.Count > 0
                        ? workspace.Examples.Select(e => (e.Grade, e.Text)).ToList()
                        : null);

                string reportText;
                try
                {
                    reportText = await ollamaClient.SendPromptAsync(fullPrompt, cancellationToken);
                }
                catch (Exception ex)
                {
                    var failed = new ProgressStep(student.Name, StepStatus.Failed, ex.Message);
                    steps.Add(failed);
                    onStudentStep?.Invoke(failed);
                    continue;
                }

                writer.AddRow(i + 1, reportText);

                var row = new ReportRowSnapshot
                {
                    SequenceNumber = i + 1,
                    StudentName = student.Name,
                    ReportText = reportText,
                    PromptText = fullPrompt,
                };
                rows.Add(row);
                onReportGenerated?.Invoke(row);

                var done = new ProgressStep(student.Name, StepStatus.Done);
                steps.Add(done);
                onStudentStep?.Invoke(done);
            }

            if (rows.Count == 0)
                throw new InvalidOperationException("No reports were generated.");

            var savedPath = await writer.SaveAsync(tempDir, cancellationToken);
            var updated = await workspaceStore.SaveReportRunAsync(workspaceId, rows, savedPath);
            return (updated, steps);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    public async Task<(WorkspaceState Workspace, IReadOnlyList<ProgressStep> Steps)> GenerateResultsAnalysisReportsAsync(
        string workspaceId,
        Action<ProgressStep>? onStudentStep = null,
        Action<ReportRowSnapshot>? onReportGenerated = null,
        CancellationToken cancellationToken = default)
    {
        var workspace = await workspaceStore.GetAsync(workspaceId);
        if (string.IsNullOrWhiteSpace(workspace.Exam.SummaryFinal))
            throw new InvalidOperationException("Process the exam first.");
        if (workspace.ResultsAnalysis.Students.Count == 0)
            throw new InvalidOperationException("Preprocess the results spreadsheet first.");

        var effectivePrompt = string.IsNullOrWhiteSpace(workspace.Prompt)
            ? reportOptions.DefaultPrompt
            : workspace.Prompt;
        var effectiveTask = string.IsNullOrWhiteSpace(workspace.Task)
            ? reportOptions.DefaultTask
            : workspace.Task;

        var tempDir = Path.Combine(Path.GetTempPath(), $"reportgen_results_run_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var steps = new List<ProgressStep>();
        var rows = new List<ReportRowSnapshot>();

        try
        {
            ollamaOptions.Model = workspace.SelectedModel;
            using var writer = new ReportWriter();

            for (var i = 0; i < workspace.ResultsAnalysis.Students.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var student = workspace.ResultsAnalysis.Students[i];
                var resultsRow = new ResultsRow(
                    student.StudentNumber,
                    student.Class,
                    student.Marks.Select(m => new QuestionMark(m.Label, m.StudentMark, m.MaxMark)).ToList(),
                    student.Total,
                    student.Percentage,
                    student.Rank,
                    student.Grade);

                var fullPrompt = promptBuilder.BuildResultsPrompt(
                    workspace.Exam.SummaryFinal!,
                    resultsRow,
                    effectivePrompt,
                    effectiveTask,
                    workspace.Examples.Count > 0
                        ? workspace.Examples.Select(e => (e.Grade, e.Text)).ToList()
                        : null);

                string reportText;
                try
                {
                    reportText = await ollamaClient.SendPromptAsync(fullPrompt, cancellationToken);
                }
                catch (Exception ex)
                {
                    var failed = new ProgressStep($"{student.StudentNumber} / {student.Class}", StepStatus.Failed, ex.Message);
                    steps.Add(failed);
                    onStudentStep?.Invoke(failed);
                    continue;
                }

                writer.AddRow(i + 1, reportText);

                var row = new ReportRowSnapshot
                {
                    SequenceNumber = i + 1,
                    StudentName = $"{student.StudentNumber} / {student.Class}",
                    ReportText = reportText,
                    PromptText = fullPrompt,
                };
                rows.Add(row);
                onReportGenerated?.Invoke(row);

                var done = new ProgressStep(row.StudentName, StepStatus.Done);
                steps.Add(done);
                onStudentStep?.Invoke(done);
            }

            if (rows.Count == 0)
                throw new InvalidOperationException("No reports were generated.");

            var savedPath = await writer.SaveAsync(tempDir, cancellationToken);
            var updated = await workspaceStore.SaveResultsReportRunAsync(workspaceId, rows, savedPath);
            return (updated, steps);
        }
        finally
        {
            TryDeleteDirectory(tempDir);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
        }
    }
}
