namespace WebToMarkdown;

internal enum ExtractionMode
{
    Full,
    KeywordStat,
    Keyword,
    Resume
}

internal readonly record struct CliOptions(ExtractionMode Mode, string Url)
{
    public static bool TryParse(string[] args, out CliOptions options, out string error)
    {
        options = default;
        error = string.Empty;

        if (args.Length == 0 || args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            error = "Usage: WebToMarkdown.exe <url> [--keywordstat|--keyword|--resume]";
            return false;
        }

        string? url = null;
        ExtractionMode mode = ExtractionMode.Full;
        var modeSeen = false;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var candidate = arg.ToLowerInvariant() switch
                {
                    "--keywordstat" => ExtractionMode.KeywordStat,
                    "--keyword" => ExtractionMode.Keyword,
                    "--resume" => ExtractionMode.Resume,
                    _ => (ExtractionMode?)null
                };

                if (!candidate.HasValue)
                {
                    error = $"Unknown parameter: {arg}";
                    return false;
                }

                if (modeSeen)
                {
                    error = "Only one extraction mode can be specified.";
                    return false;
                }

                mode = candidate.Value;
                modeSeen = true;
                continue;
            }

            if (url is not null)
            {
                error = "Only one URL can be specified.";
                return false;
            }

            url = arg;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "A URL is required.";
            return false;
        }

        options = new CliOptions(mode, url);
        return true;
    }
}

internal sealed class AiConfig
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5:1.5b";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;
}

internal sealed class ExtractionResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Published { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public string Markdown { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int ParagraphCount { get; set; }
    public bool UsedSiteRule { get; set; }
    public double ExtractionScore { get; set; }
}

internal sealed class SiteRule
{
    public string[] ContentSelectors { get; init; } = Array.Empty<string>();
    public string[] ExcludeSelectors { get; init; } = Array.Empty<string>();
}

internal sealed class ApplicationResult
{
    public int ExitCode { get; init; }
    public string OutputPath { get; init; } = string.Empty;
}
