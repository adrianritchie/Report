# Teacher Report Generator — Application Plan

## Overview

A **C# / .NET 10 CLI tool** that accepts an exam paper PDF and a teacher feedback
Excel spreadsheet, then calls a **local Ollama instance** to produce a concise
teacher assessment report for each student. Output is a single
`reports_<timestamp>.xlsx` file (one row per student) plus one `<n>.txt` prompt
file per student written to the same output directory.

The tool operates in **batch mode only** — single-student mode has been removed.

---

## Goals

| # | Goal |
|---|---|
| 1 | Accept an exam-paper PDF and a teacher feedback `.xlsx` spreadsheet from the command line |
| 2 | Send a structured prompt to a local Ollama Chat API endpoint for each student |
| 3 | Allow the teacher to supply a free-text prompt to tune tone/focus of the reports |
| 4 | Write all reports to a single `reports_<timestamp>.xlsx` file (columns: Student number, Report text) |
| 5 | Write each student's assembled prompt to `<n>.txt` before the Ollama call |
| 6 | Allow the model to be configured in `appsettings.json` and overridden per-run via `--model` |

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  CLI (dotnet run / published executable)                │
│                                                         │
│  ┌──────────────┐  ┌─────────────────┐                  │
│  │ PDF Extractor│  │ Excel Extractor │                  │
│  └──────┬───────┘  └────────┬────────┘                  │
│         │                   │                           │
│         └─────────┬─────────┘                           │
│                   │                                     │
│          ┌────────▼────────┐                            │
│          │  Prompt Builder │                            │
│          └────────┬────────┘                            │
│                   │                                     │
│          ┌────────▼────────┐                            │
│          │  Ollama Client  │                            │
│          │  POST /api/chat │                            │
│          └────────┬────────┘                            │
│                   │                                     │
│          ┌────────▼────────┐                            │
│          │  Report Writer  │  → reports_<ts>.xlsx       │
│          │  (ClosedXML)    │  → <n>.txt prompt files    │
│          └─────────────────┘                            │
└─────────────────────────────────────────────────────────┘
                   │
        Local Ollama Server
        (http://localhost:11434)
```

---

## Project Structure

```
ReportGenerator/
├── ReportGenerator.slnx                     # solution (.NET 10 .slnx format)
├── src/
│   └── ReportGenerator/
│       ├── ReportGenerator.csproj
│       ├── Program.cs                       # CLI entry point — batch mode only
│       ├── appsettings.json                 # Ollama + Report configuration
│       ├── Configuration/
│       │   └── AppOptions.cs                # OllamaOptions + ReportOptions
│       ├── Extraction/
│       │   ├── IContentExtractor.cs
│       │   ├── ContentExtractorRouter.cs    # routes by file extension
│       │   ├── PdfExtractor.cs              # PDF text extraction (PdfPig)
│       │   ├── TextFileExtractor.cs         # .txt / .md reader
│       │   ├── IExcelExtractor.cs
│       │   ├── ExcelExtractor.cs            # .xlsx batch reader (ClosedXML)
│       │   └── StudentRow.cs                # record: LastName, FirstName, Fields
│       ├── Ollama/
│       │   ├── IOllamaClient.cs
│       │   ├── OllamaClient.cs              # POST /api/chat, stream: false
│       │   └── OllamaModels.cs              # request/response DTOs
│       └── Report/
│           ├── PromptBuilder.cs             # assembles structured prompt
│           └── ReportWriter.cs              # stateful IDisposable; builds .xlsx
└── tests/
    └── ReportGenerator.Tests/
        ├── ReportGenerator.Tests.csproj
        ├── TextFileExtractorTests.cs
        ├── ContentExtractorRouterTests.cs
        ├── PromptBuilderTests.cs
        ├── ReportWriterTests.cs
        ├── OllamaClientTests.cs
        └── ExcelExtractorTests.cs
```

---

## CLI Interface

```
dotnet run -- \
  --exam        <path/to/exam.pdf>              \
  --spreadsheet <path/to/feedback.xlsx>         \
  [--prompt     "Focus on mathematical reasoning"] \
  [--output-dir <path/to/reports/>]             \
  [--model      mistral]                        \
  [--verbose]
```

| Flag | Short | Required | Description |
|---|---|---|---|
| `--exam` | `-e` | Yes | Path to exam paper PDF |
| `--spreadsheet` | `-x` | Yes | Path to `.xlsx` feedback spreadsheet |
| `--prompt` | `-p` | No | Teacher tuning prompt (falls back to `DefaultPrompt` in config) |
| `--output-dir` | `-d` | No | Directory for output files (default: `DefaultOutputDirectory` in config) |
| `--model` | `-m` | No | Ollama model name; overrides `appsettings.json` |
| `--verbose` | `-v` | No | Print extracted text and assembled prompt before sending |

---

## Key Dependencies (NuGet)

### Main project

| Package | Version | Purpose |
|---|---|---|
| `System.CommandLine` | `2.0.3` | Argument parsing |
| `PdfPig` | `0.1.13` | PDF text extraction |
| `ClosedXML` | `0.105.0` | `.xlsx` reading and writing (no Excel required) |
| `Microsoft.Extensions.Http` | `10.0.0` | `IHttpClientFactory` / typed `HttpClient` |
| `Microsoft.Extensions.Configuration.Json` | `10.0.0` | `appsettings.json` support |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | `10.0.0` | Env var overrides |
| `Microsoft.Extensions.Configuration.Binder` | `10.0.0` | Strongly-typed config binding |
| `Microsoft.Extensions.DependencyInjection` | `10.0.0` | DI container |

### Test project additional packages

`xunit 2.9.3`, `xunit.runner.visualstudio 3.1.4`, `Microsoft.NET.Test.Sdk 17.14.1`,
`coverlet.collector 6.0.4`, `ClosedXML 0.105.0` (for asserting saved `.xlsx` contents).

---

## Ollama Integration

The tool calls the Ollama Chat API with a single non-streaming request:

**Endpoint:** `POST /api/chat`

**Request body:**
```json
{
  "model": "llama3.2",
  "messages": [{ "role": "user", "content": "<assembled prompt>" }],
  "stream": false
}
```

**Response:** `{ "message": { "role": "assistant", "content": "..." }, "done": true, "done_reason": "stop" }`

No authentication is required — Ollama runs locally.

---

## Excel Spreadsheet Format

The spreadsheet must be an `.xlsx` file. The first row is the heading row; all
subsequent non-empty rows are treated as student records.

| Column | Content |
|---|---|
| 1 | Student name (full name in a single cell) |
| 2+ | Feedback fields — the heading cell provides the label; the cell value is the content |

Empty rows (name cell blank) are skipped automatically. Blank feedback cells are
included in the prompt with an empty value.

**Example:**

| Name | Written Communication | Mathematical Reasoning |
|---|---|---|
| Alice Smith | Excellent written work | Strong number sense |
| Bob Jones | Needs more detail | Solid understanding |

Each feedback field is rendered in the prompt as:
```
[Written Communication]
Excellent written work

[Mathematical Reasoning]
Strong number sense
```

---

## Batch Error Handling

When a student fails (e.g. Ollama timeout):
1. **Auto-retry once** silently
2. If still failing, prompt: `Retry [Firstname Lastname]? [Y/n]:`
   - `Y` (or Enter) — retries one more time
   - `N` — skips the student and continues the batch
3. If the user-requested retry also fails, the student is skipped and counted in the final summary

Exit code `3` is returned if any students were skipped due to errors.

---

## Prompt Structure

The prompt assembled by `PromptBuilder` follows this template:

```
[STUDENT]
<student full name>

[EXAM PAPER]
<extracted exam text>

[STUDENT RESPONSES]
[Written Communication]
<value>

[Mathematical Reasoning]
<value>

[TEACHER INSTRUCTIONS]
<teacher --prompt text, or DefaultPrompt from config>

[TASK]
You are assisting a teacher. Using the exam paper and student responses above,
write a concise, professional teacher assessment report suitable for a school
report card. The report must include:
  1. An overall performance summary (2-3 sentences).
  2. Key strengths demonstrated by the student.
  3. Specific areas for improvement with actionable suggestions.
The report should not include:
  1. A recommended grade or mark with brief justification.
Use formal but accessible language appropriate for sharing with parents and students.
Do not invent facts not evidenced in the student's responses.
Keep the report concise, ideally around 150 words.
```

---

## Report Output

### Excel file (`reports_<timestamp>.xlsx`)

- One worksheet named **Reports**
- Header row (bold): **Student** | **Report**
- One data row per student: sequential number | Ollama report text
- Column A auto-fitted; column B fixed at width 100 with wrap-text enabled

### Prompt files (`<n>.txt`)

Written to the output directory before the Ollama call for each student, so they
persist even if the Ollama call fails.

---

## Error Handling

| Scenario | Exit code | Behaviour |
|---|---|---|
| Exam or spreadsheet file not found | 1 | Print path; exit |
| Spreadsheet has no data rows | 0 | Print warning; exit cleanly |
| Spreadsheet has no columns | 1 | Print format error; exit |
| Ollama unreachable (`HttpRequestException`) | 3 | Print URL + "ensure `ollama serve` is running" |
| Ollama timeout (`TaskCanceledException`) | 3 | Print timeout value + suggest smaller model |
| Empty / whitespace response from Ollama | skip (batch) | Count as failed; apply retry logic |
| Per-student failure in batch after retries | 3 (end of batch) | Skip student; print summary |
| User cancellation (`Ctrl+C`) | 1 | Print cancelled message |

---

## Configuration (`appsettings.json`)

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

All keys can be overridden via environment variables with the prefix `REPORTGEN_`
(e.g. `REPORTGEN_Ollama__Model=mistral`). No secrets are required — Ollama is local
and unauthenticated.

---

## Current Status

The project is **feature-complete**. All planned work is implemented and tested.

| Area | Status |
|---|---|
| CLI argument parsing (`System.CommandLine 2.0.3`) — batch-only | Done |
| PDF text extraction (`PdfPig 0.1.13`) | Done |
| Plain-text / `.txt` file support | Done |
| Content extractor router (PDF vs text by extension) | Done |
| Excel spreadsheet reader (`ClosedXML`) | Done |
| Prompt builder (structured template, no grade recommendation) | Done |
| Ollama Chat API client (`POST /api/chat`, `stream: false`) | Done |
| Per-student prompt file written as `<n>.txt` before Ollama call | Done |
| Report writer — stateful `IDisposable`, saves `reports_<ts>.xlsx` | Done |
| `--model` flag overriding appsettings | Done |
| Per-student retry with interactive prompt; exit code 3 on skips | Done |
| Unit tests — 31 passing, 0 failing | Done |

### Test coverage

| Test file | Covers |
|---|---|
| `TextFileExtractorTests.cs` | Plain-text extraction |
| `ContentExtractorRouterTests.cs` | Routing by file extension |
| `PromptBuilderTests.cs` | Prompt template assembly |
| `ReportWriterTests.cs` | Excel file creation, header row, data rows, filename prefix |
| `OllamaClientTests.cs` | HTTP happy path, model/stream/message sent, all error paths |
| `ExcelExtractorTests.cs` | Happy path, empty rows skipped, missing file, no data rows, too few columns, blank cells |

---

## Out of Scope (for now)

- Web or Teams UI
- Grade database / persistence
- Mark-scheme aware grading (future: upload a mark scheme as a third PDF)
- Streaming responses from Ollama
