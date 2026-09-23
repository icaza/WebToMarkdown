# WebToMarkdown

Windows command-line web extraction utility built with C# 10+ / .NET 10 and Microsoft WebView2.

The process has no visible console or GUI. It accepts a URL, renders the page with WebView2, extracts the rendered DOM rather than the HTML source, and writes a Markdown report into `reports_`.

## Usage

```text
WebToMarkdown.exe <url>
WebToMarkdown.exe <url> --keywordstat
WebToMarkdown.exe <url> --keyword
WebToMarkdown.exe <url> --resume
```

`--keywordstat` creates Unicode-aware word statistics.

`--keyword` writes only the extracted tokens, one per line, with no statistical header.

`--resume` first tries the configured OpenAI-compatible endpoint. If AI is disabled, unavailable, times out, or returns an error, a local extractive summary is produced automatically.

## Configuration

`config.json` is next to the executable:

```json
{
  "Enabled": false,
  "BaseUrl": "http://localhost:11434",
  "Model": "qwen2.5:1.5b",
  "ApiKey": "Instead, set the OLLAMA_API_KEY environment variable to your API key....",
  "TimeoutSeconds": 20
}
```

For Ollama, the application automatically targets `/v1/chat/completions` when `BaseUrl` is `http://localhost:11434` and also accepts a base URL that already ends in `/v1` or `/chat/completions`.

## Output and logs

Reports are written to:

```text
reports_\\
```

Application errors and diagnostics are written to:

```text
logs_\\app.log
```

WebView2 uses a temporary per-run profile under the Windows temporary directory. The profile is deleted after the run so cookies, cache and other browser state are not retained between executions.

## Extraction strategy

The extractor combines:

1. Site-specific content selectors for common documentation, article, reference, source-code and discussion sites.
2. Generic semantic candidates such as `article`, `main`, `[role=main]`, `articleBody`, and content-like containers.
3. A scoring function using visible text size, paragraph count, link density and content-related class/id signals.
4. Semantic Markdown rendering for headings, paragraphs, lists, quotes, code, tables, links, figures and definition lists.
5. Generic boilerplate removal for navigation, advertisements, cookie dialogs, consent widgets, social blocks, pagination and related-content blocks.

The webpage is treated as untrusted input. The AI prompt explicitly separates page content from instructions, and the host does not expose native host objects or WebMessages to page JavaScript.

## Requirements

- Windows 10/11 with Microsoft Edge WebView2 Runtime installed.
- .NET 10 runtime, unless published self-contained.
- x64 target in the supplied project.

## Build

```powershell
dotnet restore
dotnet build .\\WebToMarkdown.sln -c Release -p:Platform=x64
```

## Publish self-contained

```powershell
dotnet publish .\\src\\WebToMarkdown\\WebToMarkdown.csproj -c Release -r win-x64 --self-contained true
```

The WebView2 Runtime itself is not bundled by this project. The Evergreen Runtime is the expected deployment model.
