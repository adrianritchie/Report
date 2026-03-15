using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportGenerator.Configuration;
using ReportGenerator.Extraction;
using ReportGenerator.Ollama;
using ReportGenerator.Report;

// ── Configuration ────────────────────────────────────────────────────────────

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables("REPORTGEN_")
    .Build();

var ollamaOptions = configuration
    .GetSection(OllamaOptions.SectionName)
    .Get<OllamaOptions>() ?? new OllamaOptions();

var reportOptions = configuration
    .GetSection(ReportOptions.SectionName)
    .Get<ReportOptions>() ?? new ReportOptions();

// ── Services ─────────────────────────────────────────────────────────────────

var services = new ServiceCollection();

services.AddSingleton(ollamaOptions);
services.AddSingleton(reportOptions);
services.AddSingleton<IContentExtractor, ContentExtractorRouter>();
services.AddSingleton<IExcelExtractor, ExcelExtractor>();
services.AddSingleton<PromptBuilder>();

services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    client.BaseAddress = new Uri(ollamaOptions.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(ollamaOptions.TimeoutSeconds);
});

var serviceProvider = services.BuildServiceProvider();

// ── CLI Definition (System.CommandLine 2.0.3) ────────────────────────────────

var examOpt = new Option<FileInfo>("--exam", new[] { "-e" })
{
    Description = "Path to the exam paper (PDF).",
    Required = true
};

var spreadsheetOpt = new Option<FileInfo>("--spreadsheet", new[] { "-x" })
{
    Description = "Path to an Excel (.xlsx) spreadsheet with one student per row.",
    Required = true
};

var promptOpt = new Option<string?>("--prompt", new[] { "-p" })
{
    Description = "Optional teacher instructions to tune the report."
};

var outputDirOpt = new Option<DirectoryInfo?>("--output-dir", new[] { "-d" })
{
    Description = "Directory for the output .xlsx file. Defaults to DefaultOutputDirectory in appsettings.json."
};

var modelOpt = new Option<string?>("--model", new[] { "-m" })
{
    Description = $"Ollama model name to use. Overrides appsettings.json. (default: {ollamaOptions.Model})"
};

var verboseOpt = new Option<bool>("--verbose", new[] { "-v" })
{
    Description = "Print extracted text and assembled prompt before sending."
};

var rootCommand = new RootCommand(
    "Generates teacher assessment reports using a local Ollama model.\n" +
    "\nExample:\n" +
    "  dotnet run -- --exam exam.pdf --spreadsheet feedback.xlsx --output-dir ./reports");

rootCommand.Add(examOpt);
rootCommand.Add(spreadsheetOpt);
rootCommand.Add(promptOpt);
rootCommand.Add(outputDirOpt);
rootCommand.Add(modelOpt);
rootCommand.Add(verboseOpt);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var exam        = parseResult.GetValue(examOpt)!;
    var spreadsheet = parseResult.GetValue(spreadsheetOpt)!;
    var prompt      = parseResult.GetValue(promptOpt);
    var outputDir   = parseResult.GetValue(outputDirOpt);
    var model       = parseResult.GetValue(modelOpt);
    var verbose     = parseResult.GetValue(verboseOpt);

    // --model flag overrides appsettings value
    if (!string.IsNullOrWhiteSpace(model))
        ollamaOptions.Model = model;

    return await RunBatchAsync(exam, spreadsheet, prompt, outputDir, verbose, cancellationToken);
});

return await rootCommand.Parse(args).InvokeAsync();

// ── Batch Mode ────────────────────────────────────────────────────────────────

async Task<int> RunBatchAsync(
    FileInfo examFile,
    FileInfo spreadsheetFile,
    string? teacherPrompt,
    DirectoryInfo? outputDirectory,
    bool verbose,
    CancellationToken cancellationToken)
{
    try
    {
        if (!examFile.Exists)
        {
            Console.Error.WriteLine($"[Error] Exam file not found: {examFile.FullName}");
            return 1;
        }

        if (!spreadsheetFile.Exists)
        {
            Console.Error.WriteLine($"[Error] Spreadsheet not found: {spreadsheetFile.FullName}");
            return 1;
        }

        var effectivePrompt = !string.IsNullOrWhiteSpace(teacherPrompt)
            ? teacherPrompt
            : reportOptions.DefaultPrompt;

        var outDir = outputDirectory?.FullName ?? reportOptions.DefaultOutputDirectory;

        Console.WriteLine("Report Generator v1.0");
        Console.WriteLine($"  Exam        : {examFile.Name}");
        Console.WriteLine($"  Spreadsheet : {spreadsheetFile.Name}");
        Console.WriteLine($"  Model       : {ollamaOptions.Model}");
        Console.WriteLine($"  Output dir  : {outDir}");
        Console.WriteLine();

        // Extract exam text once
        var extractor = serviceProvider.GetRequiredService<IContentExtractor>();
        Console.Write("Extracting exam paper text...");
        var examText = await extractor.ExtractAsync(examFile.FullName, cancellationToken);
        Console.WriteLine($" {examText.Length} characters extracted.");

        if (verbose)
            PrintVerboseSection("EXAM PAPER TEXT", examText);

        // Load spreadsheet
        Console.Write("Reading spreadsheet...");
        var excelExtractor = serviceProvider.GetRequiredService<IExcelExtractor>();
        var students = excelExtractor.Extract(spreadsheetFile.FullName);
        Console.WriteLine($" {students.Count} student(s) found.");
        Console.WriteLine();

        if (students.Count == 0)
        {
            Console.Error.WriteLine("[Warning] Spreadsheet contains no student rows. Nothing to process.");
            return 0;
        }

        var promptBuilder = serviceProvider.GetRequiredService<PromptBuilder>();
        var ollamaClient  = serviceProvider.GetRequiredService<IOllamaClient>();

        using var writer = new ReportWriter();

        var succeeded = 0;
        var failed    = 0;

        for (var i = 0; i < students.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Console.Error.WriteLine("[Cancelled] Operation was cancelled.");
                break;
            }

            var s = students[i];
            Console.Write($"[{i + 1}/{students.Count}] {s.FullName}...");

            // Build responses text from spreadsheet fields
            var responsesText = BuildResponsesFromFields(s);

            if (verbose)
                PrintVerboseSection($"STUDENT RESPONSES — {s.FullName}", responsesText);

            var assembledPrompt = promptBuilder.Build(examText, responsesText, effectivePrompt, s.FullName);

            if (verbose)
                PrintVerboseSection($"ASSEMBLED PROMPT — {s.FullName}", assembledPrompt);

            await WritePromptFileAsync(assembledPrompt, outDir, i + 1, cancellationToken);

            var reportText = await TryGenerateReportAsync(ollamaClient, assembledPrompt, s, cancellationToken);

            if (reportText is not null)
            {
                writer.AddRow(i + 1, reportText);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" done.");
                Console.ResetColor();
                succeeded++;
            }
            else
            {
                failed++;
            }
        }

        if (succeeded > 0)
        {
            var savedPath = await writer.SaveAsync(outDir, cancellationToken);
            Console.WriteLine();
            Console.ForegroundColor = succeeded == students.Count ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine($"Completed {succeeded}/{students.Count} student(s).");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Report saved to: {savedPath}");
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No reports were generated.");
        }

        if (failed > 0)
            Console.WriteLine($"  {failed} student(s) were skipped due to errors.");

        Console.ResetColor();

        return failed == 0 ? 0 : 3;
    }
    catch (FileNotFoundException ex)
    {
        Console.Error.WriteLine($"[Error] File not found: {ex.FileName}");
        return 1;
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine($"[Error] {ex.Message}");
        return 1;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("[Cancelled] Operation was cancelled.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Unexpected Error] {ex.GetType().Name}: {ex.Message}");
        if (verbose)
            Console.Error.WriteLine(ex.StackTrace);
        return 1;
    }
}

// ── Batch helpers ─────────────────────────────────────────────────────────────

/// <summary>
/// Tries to generate a report for one student.
/// On failure: auto-retries once silently, then prompts the user.
/// Returns the report text on success, or null if skipped.
/// </summary>
async Task<string?> TryGenerateReportAsync(
    IOllamaClient ollamaClient,
    string assembledPrompt,
    StudentRow student,
    CancellationToken cancellationToken)
{
    // Attempt 1 (initial) + Attempt 2 (auto-retry) + Attempt 3 (user-requested retry)
    for (var attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            var reportText = await ollamaClient.SendPromptAsync(assembledPrompt, cancellationToken);

            if (string.IsNullOrWhiteSpace(reportText))
                throw new InvalidOperationException("Ollama returned an empty response.");

            return reportText;
        }
        catch (OperationCanceledException)
        {
            throw; // propagate cancellation
        }
        catch (Exception ex)
        {
            if (attempt == 1)
            {
                // Silent auto-retry
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [Retry 1] Error for {student.FullName}: {ex.Message}. Retrying...");
                Console.ResetColor();
                continue;
            }

            if (attempt == 2)
            {
                // Auto-retry also failed — ask the user
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [Error] {student.FullName}: {ex.Message}");
                Console.ResetColor();
                Console.Write($"  Retry {student.FullName}? [Y/n]: ");
                var answer = Console.ReadLine()?.Trim().ToUpperInvariant();
                if (answer is "" or "Y")
                    continue; // attempt 3
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  Skipping {student.FullName}.");
                    Console.ResetColor();
                    return null;
                }
            }

            // attempt == 3: user-requested retry also failed
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [Failed] {student.FullName}: {ex.Message}. Skipping.");
            Console.ResetColor();
            return null;
        }
    }

    return null; // unreachable but satisfies compiler
}

/// <summary>
/// Builds a responses text block from spreadsheet fields.
/// Each field is rendered as "[Heading]\n&lt;value&gt;\n\n".
/// </summary>
string BuildResponsesFromFields(StudentRow student)
{
    var sb = new System.Text.StringBuilder();
    foreach (var (heading, value) in student.Fields)
    {
        sb.AppendLine($"[{heading}]");
        sb.AppendLine(value);
        sb.AppendLine();
    }
    return sb.ToString().Trim();
}

// ── Shared helpers ────────────────────────────────────────────────────────────

/// <summary>Writes the assembled prompt for one student to &lt;n&gt;.txt in the output directory.</summary>
async Task WritePromptFileAsync(string prompt, string outputDirectory, int sequenceNumber, CancellationToken cancellationToken)
{
    try
    {
        Directory.CreateDirectory(outputDirectory);
        var filePath = Path.Combine(outputDirectory, $"{sequenceNumber}.txt");
        await File.WriteAllTextAsync(filePath, prompt, cancellationToken);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Error.WriteLine($"  [Warning] Could not write prompt file for student {sequenceNumber}: {ex.Message}");
        Console.ResetColor();
    }
}

void PrintVerboseSection(string title, string content)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"\n── {title} ──────────────────────────────");
    Console.WriteLine(content.Length > 2000 ? content[..2000] + "\n[...truncated]" : content);
    Console.WriteLine("─────────────────────────────────────────────");
    Console.ResetColor();
}
