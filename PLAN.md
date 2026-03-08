# Teacher Report Generator — Application Plan

## Overview

A **C# / .NET 10 CLI tool** that accepts an exam paper PDF, student responses
(PDF or plain text), and an optional teacher prompt, then calls a **local Ollama
instance** to produce a concise teacher assessment report. Output is written to
stdout and saved as a `.txt` file.

---

## Goals

| # | Goal |
|---|---|
| 1 | Accept an exam-paper PDF and student responses (PDF or `.txt`) from the command line |
| 2 | Accept a teacher feedback spreadsheet (`.xlsx`) for batch processing of multiple students |
| 3 | Allow the teacher to supply a free-text prompt to tune tone/focus of the report |
| 4 | Send the assembled prompt to a local Ollama Chat API endpoint (no auth required) |
| 5 | Output the report to the console and save it as a `.txt` file; one file per student in batch mode |
| 6 | Allow the model to be configured in `appsettings.json` and overridden per-run via `--model` |

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  CLI (dotnet run / published executable)                │
│                                                         │
│  ┌──────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ PDF      │  │ Text Input   │  │ Prompt           │  │
│  │ Extractor│  │ Reader       │  │ Builder          │  │
│  └────┬─────┘  └──────┬───────┘  └────────┬─────────┘  │
│       │               │                   │             │
│       └───────────────┴───────────────────┘             │
│                        │                               │
│  ┌─────────────────┐   │                               │
│  │ Excel Extractor │───┘ (batch mode)                  │
│  └─────────────────┘                                   │
│                        │                               │
│               ┌────────▼────────┐                      │
│               │  Ollama Client  │                      │
│               │  POST /api/chat │                      │
│               └────────┬────────┘                      │
│                        │                               │
│               ┌────────▼────────┐                      │
│               │ Report Writer   │  → stdout            │
│               │                 │  → <report>.txt      │
│               └─────────────────┘                      │
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
│       ├── Program.cs                       # CLI entry point, argument parsing
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
│           └── ReportWriter.cs              # console + .txt output
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

### Single-student mode

```
dotnet run -- \
  --exam       <path/to/exam.pdf>              \
  --responses  <path/to/responses.pdf|.txt>    \
  [--prompt    "Focus on mathematical reasoning"] \
  [--output    <path/to/report.txt>]           \
  [--student   "Alice Smith"]                  \
  [--model     mistral]                        \
  [--verbose]
```

### Batch mode (Excel spreadsheet)

```
dotnet run -- \
  --exam        <path/to/exam.pdf>              \
  --spreadsheet <path/to/feedback.xlsx>         \
  [--prompt     "Focus on mathematical reasoning"] \
  [--output-dir <path/to/reports/>]             \
  [--model      mistral]                        \
  [--verbose]
```

`--responses` and `--spreadsheet` are mutually exclusive. One of them is required.

| Flag | Short | Mode | Required | Description |
|---|---|---|---|---|
| `--exam` | `-e` | Both | Yes | Path to exam paper PDF |
| `--responses` | `-r` | Single | One of | Path to student responses PDF or `.txt` |
| `--spreadsheet` | `-x` | Batch | One of | Path to `.xlsx` feedback spreadsheet |
| `--prompt` | `-p` | Both | No | Teacher tuning prompt (falls back to `DefaultPrompt`) |
| `--output` | `-o` | Single | No | Output `.txt` path (default: `<student>_<timestamp>.txt`) |
| `--output-dir` | `-d` | Batch | No | Directory for report files (default: `DefaultOutputDirectory`) |
| `--student` | `-s` | Single | No | Student name for the report header |
| `--model` | `-m` | Both | No | Ollama model name; overrides `appsettings.json` |
| `--verbose` | `-v` | Both | No | Print extracted text and assembled prompt before sending |

---

## Key Dependencies (NuGet)

| Package | Version | Purpose |
|---|---|---|
| `System.CommandLine` | `2.0.3` | Argument parsing |
| `UglyToad.PdfPig` | `0.1.9-alpha001-patch1` | PDF text extraction |
| `UglyToad.PdfPig.Core/Fonts/Tokenization/Tokens` | `1.7.0-custom-5` | PdfPig sub-packages |
| `ClosedXML` | `0.105.0` | `.xlsx` spreadsheet reading (no Excel required) |
| `Microsoft.Extensions.Http` | `10.0.0` | `IHttpClientFactory` / typed `HttpClient` |
| `Microsoft.Extensions.Configuration.Json` | `10.0.0` | `appsettings.json` support |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | `10.0.0` | Env var overrides |
| `Microsoft.Extensions.Configuration.Binder` | `10.0.0` | Strongly-typed config binding |
| `Microsoft.Extensions.DependencyInjection` | `10.0.0` | DI container |

Test project additional packages: `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.4`, `Microsoft.NET.Test.Sdk 17.14.1`, `coverlet.collector 6.0.4`.

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

## Excel Spreadsheet Format (Batch Mode)

The spreadsheet must be an `.xlsx` file. The first row is the heading row; all subsequent non-empty rows are treated as student records.

| Column | Content |
|---|---|
| 1 | Last name |
| 2 | First name |
| 3+ | Feedback fields — the heading cell provides the label; the cell value is the content |

Empty rows (both name cells blank) are skipped automatically. Blank feedback cells are included in the prompt with an empty value.

**Example:**

| Last Name | First Name | Written Communication | Mathematical Reasoning |
|---|---|---|---|
| Smith | Alice | Excellent written work | Strong number sense |
| Jones | Bob | Needs more detail | Solid understanding |

Each feedback field is rendered in the prompt as:
```
[Written Communication]
Excellent written work

[Mathematical Reasoning]
Strong number sense
```

### Batch error handling

When a student fails (e.g. Ollama timeout):
1. **Auto-retry once** silently
2. If still failing, prompt: `Retry [Firstname Lastname]? [Y/n]:`
   - `Y` (or Enter) — retries one more time
   - `N` — skips the student and continues the batch
3. If the user-requested retry also fails, the student is skipped and counted in the final summary

Exit code `3` is returned if any students were skipped due to errors.

---

## Prompt Structure

The prompt assembled by `PromptBuilder` follows this template (batch mode prepends fields from the spreadsheet as the responses block):

```
[STUDENT]
<student name, if provided>

[EXAM PAPER]
<extracted exam text>

[STUDENT RESPONSES]
<extracted responses text  —OR—  labelled spreadsheet fields>

[TEACHER INSTRUCTIONS]
<teacher --prompt text, or DefaultPrompt from config>

[TASK]
You are assisting a teacher. Using the exam paper and student responses above,
write a concise, professional teacher assessment report suitable for a school
report card. The report must include:
  1. An overall performance summary (2-3 sentences).
  2. Key strengths demonstrated by the student.
  3. Specific areas for improvement with actionable suggestions.
  4. A recommended grade or mark with brief justification.
Use formal but accessible language. Do not invent facts not evidenced in the
student's responses.
```

In batch mode the `[STUDENT RESPONSES]` section is built from the spreadsheet fields:

```
[Written Communication]
Excellent work…

[Mathematical Reasoning]
Needs improvement…
```

---

## Report Output

The `.txt` file and console output share the same format:

```
=============================================
 TEACHER ASSESSMENT REPORT
 Student : Alice Smith
 Date    : 2026-03-04
=============================================

[Report body from Ollama]

=============================================
 Generated by ReportGenerator v1.0
=============================================
```

---

## Error Handling

| Scenario | Exit code | Behaviour |
|---|---|---|
| Exam or responses file not found | 1 | Print path; exit |
| Spreadsheet not found | 1 | Print path; exit |
| `--responses` and `--spreadsheet` both provided | 1 | Print mutual-exclusivity error; exit |
| Neither `--responses` nor `--spreadsheet` provided | 1 | Print usage hint; exit |
| Unsupported file type | 1 | Print supported formats; exit |
| Spreadsheet has fewer than 2 columns | 1 | Print format error; exit |
| Spreadsheet has no data rows | 1 | Print warning; exit 0 |
| Ollama unreachable (`HttpRequestException`) | 3 | Print URL + "ensure `ollama serve` is running" |
| Ollama timeout (`TaskCanceledException`) | 3 | Print timeout value + suggest smaller model |
| Empty / whitespace response from Ollama | 4 (single) / skip (batch) | Warn; exit or continue |
| Per-student failure in batch after retries | 3 (end of batch) | Skip student; print summary |
| Output file not writable | — | Print to stdout only; log warning |
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
    "DefaultPrompt": "Provide a balanced, constructive assessment suitable for a school report card. Highlight strengths, identify areas for improvement, and suggest a recommended grade."
  }
}
```

All keys can be overridden via environment variables with the prefix `REPORTGEN_`
(e.g. `REPORTGEN_Ollama__Model=mistral`). No secrets are required — Ollama is local and unauthenticated.

---

## Current Status

The project is **feature-complete**. All planned work is implemented and tested.

| Area | Status |
|---|---|
| CLI argument parsing (`System.CommandLine 2.0.3`) | Done |
| PDF text extraction (`PdfPig`) | Done |
| Plain-text / `.txt` file support | Done |
| Content extractor router (PDF vs text by extension) | Done |
| Excel spreadsheet reader (`ClosedXML`) — batch mode | Done |
| Prompt builder (structured template) | Done |
| Ollama Chat API client (`POST /api/chat`, `stream: false`) | Done |
| Report writer (console + `.txt` file) | Done |
| `--model` flag overriding appsettings | Done |
| Batch mode: per-student retry with interactive prompt | Done |
| Unit tests — 31 passing, 0 failing | Done |

### Test coverage

| Test file | Covers |
|---|---|
| `TextFileExtractorTests.cs` | Plain-text extraction |
| `ContentExtractorRouterTests.cs` | Routing by file extension |
| `PromptBuilderTests.cs` | Prompt template assembly |
| `ReportWriterTests.cs` | Console + file output, filename generation |
| `OllamaClientTests.cs` | HTTP happy path, model/stream/message sent, all error paths |
| `ExcelExtractorTests.cs` | Happy path, empty rows skipped, missing file, no data rows, too few columns, blank cells |

---

## Out of Scope (for now)

- Batch / multi-student mode
- Web or Teams UI
- Grade database / persistence
- Mark-scheme aware grading (future: upload a mark scheme as a third PDF)
- Streaming responses from Ollama
