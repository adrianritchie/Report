# AGENTS.md — Agent Guide for This Repository

This is a C# / .NET 10 **Blazor Server** web application that generates teacher assessment reports
via a local Ollama instance. The user uploads an exam paper (PDF/TXT) and a teacher feedback Excel
spreadsheet, selects a model, edits the prompt, and watches per-student reports generate live in the
browser. Completed reports can be downloaded as a single `reports_<timestamp>.xlsx` file.

---

## Project Layout

```
ReportGenerator/
├── ReportGenerator.slnx                          # solution (.NET 10 .slnx format)
├── src/
│   ├── ReportGenerator.Core/                     # shared business logic class library
│   │   ├── ReportGenerator.Core.csproj
│   │   ├── Configuration/AppOptions.cs           # OllamaOptions, ReportOptions
│   │   ├── Extraction/                           # PDF/text/Excel extractors + StudentRow
│   │   ├── Ollama/                               # IOllamaClient, OllamaClient, OllamaModels
│   │   └── Report/                               # PromptBuilder, ReportWriter
│   └── ReportGenerator.Web/                      # Blazor Server web app
│       ├── ReportGenerator.Web.csproj
│       ├── Program.cs                            # DI wiring + /download/{token} endpoint
│       ├── appsettings.json
│       ├── Services/DownloadTokenStore.cs        # token → file path mapping
│       ├── Models/Models.cs                      # ProgressEntry, ReportRow records
│       └── Components/
│           ├── App.razor / Routes.razor / _Imports.razor
│           ├── Layout/MainLayout.razor
│           └── Pages/
│               ├── Home.razor                    # main orchestration page
│               └── Components/
│                   ├── ModelSelector.razor       # calls ListModelsAsync on init
│                   ├── PromptEditor.razor
│                   ├── FileUploadPanel.razor
│                   ├── ProgressLog.razor
│                   ├── ResultsTable.razor
│                   └── DownloadButton.razor
└── tests/ReportGenerator.Tests/                  # xUnit tests (33 tests)
```

---

## Build / Test Commands

> **IMPORTANT — environment note.**
> `dotnet` is on the PATH as `/c/Program Files/dotnet/dotnet`. Use it directly as `dotnet`.
> Pass **Windows-style paths** (forward or back slashes both work) as arguments.

### Build Core

```bash
dotnet build "D:/Projects/Reports/ReportGenerator/src/ReportGenerator.Core/ReportGenerator.Core.csproj"
```

### Build Web

```bash
dotnet build "D:/Projects/Reports/ReportGenerator/src/ReportGenerator.Web/ReportGenerator.Web.csproj"
```

### Run all tests

```bash
dotnet test "D:/Projects/Reports/ReportGenerator/tests/ReportGenerator.Tests/ReportGenerator.Tests.csproj" --verbosity minimal
```

Expected: `Passed! - Failed: 0, Passed: 33, ...`

### Run a single test (by name substring)

```bash
dotnet test "D:/Projects/Reports/ReportGenerator/tests/ReportGenerator.Tests/ReportGenerator.Tests.csproj" --filter "DisplayName~MyTestName"
```

### Restore packages (if needed)

```bash
dotnet restore "D:/Projects/Reports/ReportGenerator/src/ReportGenerator.Core/ReportGenerator.Core.csproj"
```

---

## Key Technology Notes

- **.NET 10.0.103** at `C:\Program Files\dotnet\dotnet.exe`
- **Solution format**: `.slnx` (new in .NET 10, not `.sln`)
- **`PdfPig`** `0.1.13` (official NuGet package — replaces the former custom/forked packages)
- **Ollama Chat API**: `POST /api/chat` with `{ model, messages, stream: false }` — no auth required (local only)
- **Ollama Tags API**: `GET /api/tags` — returns available model names
- **`TaskCanceledException`** is thrown by .NET when `HttpClient.Timeout` is exceeded (not `TimeoutException`)

---

## Required Configuration (appsettings.json / env vars)

Env var prefix: `REPORTGEN_`

| Section | Key | Description |
|---|---|---|
| `Ollama` | `BaseUrl` | Ollama server base URL (default `http://localhost:11434/`) |
| `Ollama` | `Model` | Model name to use (default `llama3.2`) |
| `Ollama` | `TimeoutSeconds` | HTTP timeout in seconds (default `120`) |
| `Report` | `DefaultPrompt` | Fallback prompt if none supplied |
| `Report` | `DefaultOutputDirectory` | Where to save the output `.xlsx` file |

Never commit real values — use environment variables or a local `appsettings.Local.json` (git-ignored).

---

## Code Style Conventions

These defaults apply until the project defines a formatter or linter config.

### Formatting

- Always use the project's configured formatter (Prettier, Black, `gofmt`, `rustfmt`, etc.) — never manually reformat.
- Lines should be readable; avoid dense one-liners.
- Trailing commas: use them in JS/TS/Python collections and parameter lists where the language allows.

### Imports

Order from most general to most specific:
1. Standard library
2. Third-party packages
3. Internal/workspace modules
4. Relative imports

Keep groups separated by a blank line. Remove unused imports.

### Naming

| Construct | Convention | Example |
|---|---|---|
| Types / Classes | PascalCase nouns | `UserAccount`, `OrderItem` |
| Functions / Methods | camelCase verbs (JS/TS/Java) or snake_case (Python/Go/Rust) | `fetchUser`, `parse_config` |
| Constants | UPPER_SNAKE or PascalCase depending on language | `MAX_RETRIES` |
| Booleans | Predicate form | `isReady`, `hasItems`, `shouldRetry` |

Avoid abbreviations unless universally understood (`id`, `url`, `ctx`, `err`).

### Types and Public APIs

- Prefer explicit types at all module/package boundaries.
- Avoid `any` (TS), bare `object` (Python), or `interface{}` (Go) unless isolated and justified.
- Model domain concepts with named types; avoid passing raw maps/dicts across boundaries.
- Express invariants with guards/assertions at entry points, not buried comments.

### Error Handling

- Validate inputs early; fail fast at boundaries rather than deep in helpers.
- Include context in errors: operation name + relevant identifiers (e.g., `"fetchUser id=42: not found"`).
- Do not swallow errors silently; only catch when you can add meaning or offer recovery.
- Use typed/structured error types where the language supports it.
- Keep error messages stable (suitable for logs and alerts).

### Logging

- Log at boundaries (API handlers, job runners, CLI entry points), not inside utility functions.
- Include correlation IDs / request IDs where available.
- Never log secrets, tokens, passwords, or raw PII.

### Tests

- Unit-test pure logic; use integration tests for IO/network flows.
- Tests must be deterministic — mock time and network dependencies.
- Structure: **Arrange → Act → Assert** with clear separation.
- Prefer table-driven / parameterized tests for similar cases.
- Test names should describe the scenario, not the implementation.

### Performance and Safety

- Optimize only with evidence (profiling, benchmarks); prefer clarity first.
- Avoid global mutable state.
- Treat all external input (user input, files, network) as untrusted; validate and bound sizes.

---

## Git / PR Hygiene

- Keep commits scoped to the requested task; do not reformat unrelated files.
- Write commit messages in the imperative mood: `"Add retry logic for HTTP client"`.
- Update tests and documentation alongside behavior changes.
- Prefer small, reviewable commits over large monolithic ones.
- Never commit secrets, tokens, or credentials.

---

## Updating This File

When project commands or conventions change, update the relevant sections above with:
- Exact one-liners for install / build / lint / test
- How to run a single test (file path + test name filter)
- Required env var names (names only — never values)
- How to reproduce CI checks locally
