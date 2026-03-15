# Report Generator

A C# / .NET 10 CLI tool that generates teacher assessment reports using a local [Ollama](https://ollama.com) instance.

Given an exam paper PDF and a teacher feedback Excel spreadsheet, it calls Ollama once per student and writes:
- A single **`reports_<timestamp>.xlsx`** file with one row per student
- One **`<n>.txt`** prompt file per student (written before the Ollama call)

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com) running locally (`ollama serve`)
- A model pulled, e.g. `ollama pull llama3.2`

---

## Quick start

```bash
dotnet run --project ReportGenerator/src/ReportGenerator -- \
  --exam        path/to/exam.pdf        \
  --spreadsheet path/to/feedback.xlsx  \
  --output-dir  ./reports
```

---

## CLI flags

| Flag | Short | Required | Description |
|---|---|---|---|
| `--exam` | `-e` | Yes | Path to the exam paper PDF |
| `--spreadsheet` | `-x` | Yes | Path to the `.xlsx` teacher feedback spreadsheet |
| `--prompt` | `-p` | No | Free-text teacher instructions to tune the report (falls back to `DefaultPrompt` in config) |
| `--output-dir` | `-d` | No | Directory for output files (default: `DefaultOutputDirectory` in `appsettings.json`) |
| `--model` | `-m` | No | Ollama model name; overrides `appsettings.json` |
| `--verbose` | `-v` | No | Print extracted text and assembled prompt to stdout before sending |

---

## Spreadsheet format

The first row is the heading row. Subsequent non-empty rows are student records.

| Column | Content |
|---|---|
| 1 | Student name (full name in a single cell) |
| 2+ | Feedback fields — heading provides the label; cell value is the content |

Empty rows (name cell blank) are skipped automatically.

---

## Output

### `reports_<timestamp>.xlsx`

| Student | Report |
|---|---|
| 1 | *Ollama-generated report text for student 1* |
| 2 | *Ollama-generated report text for student 2* |

### `<n>.txt`

The full assembled prompt sent to Ollama for each student, written before the API call so it persists even on failure.

---

## Configuration

Edit `ReportGenerator/src/ReportGenerator/appsettings.json`:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2",
    "TimeoutSeconds": 120
  },
  "Report": {
    "DefaultOutputDirectory": ".",
    "DefaultPrompt": "Provide a balanced, constructive assessment suitable for a school report card. Highlight strengths, identify areas for improvement. Keep the output to a maximum of 150 words. Use a positive and encouraging tone."
  }
}
```

All keys can be overridden with environment variables using the prefix `REPORTGEN_`
(e.g. `REPORTGEN_Ollama__Model=mistral`).

---

## Error handling

On a per-student Ollama failure the tool:
1. Auto-retries once silently
2. Prompts `Retry <student>? [Y/n]:` if the retry also fails
3. Skips the student if declined or if the user-requested retry fails

Exit code `3` is returned if any students were skipped. Exit code `0` means all students succeeded.

---

## Build & test

```bash
# Build
dotnet build ReportGenerator/src/ReportGenerator/ReportGenerator.csproj

# Run all tests (31 tests)
dotnet test ReportGenerator/tests/ReportGenerator.Tests/ReportGenerator.Tests.csproj --verbosity minimal
```

---

## Project structure

```
ReportGenerator/
├── ReportGenerator.slnx
├── src/ReportGenerator/
│   ├── Program.cs                 # CLI entry point
│   ├── appsettings.json
│   ├── Configuration/             # OllamaOptions, ReportOptions
│   ├── Extraction/                # PDF + Excel extractors, StudentRow
│   ├── Ollama/                    # OllamaClient (POST /api/chat)
│   └── Report/                    # PromptBuilder, ReportWriter
└── tests/ReportGenerator.Tests/   # xUnit test project (31 tests)
```
