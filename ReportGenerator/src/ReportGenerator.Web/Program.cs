using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportGenerator.Configuration;
using ReportGenerator.Extraction;
using ReportGenerator.Ollama;
using ReportGenerator.Report;
using ReportGenerator.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Bind config
var ollamaOptions = builder.Configuration
    .GetSection(OllamaOptions.SectionName)
    .Get<OllamaOptions>() ?? new OllamaOptions();

var reportOptions = builder.Configuration
    .GetSection(ReportOptions.SectionName)
    .Get<ReportOptions>() ?? new ReportOptions();

builder.Services.AddSingleton(ollamaOptions);
builder.Services.AddSingleton(reportOptions);

builder.Services.AddSingleton<IContentExtractor, ContentExtractorRouter>();
builder.Services.AddSingleton<IExcelExtractor, ExcelExtractor>();
builder.Services.AddSingleton<PromptBuilder>();
builder.Services.AddSingleton<DownloadTokenStore>();

builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<OllamaOptions>();
    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ReportGenerator.Web.Components.App>()
    .AddInteractiveServerRenderMode();

// Download endpoint: resolves a one-time token to a temp file path
app.MapGet("/download/{token}", (string token, DownloadTokenStore store) =>
    Results.File(
        store.Resolve(token),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "reports.xlsx"));

app.Run();
