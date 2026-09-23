using System.Diagnostics;
using System.Text;

namespace WebToMarkdown;

internal static class ExtractionApplication
{
    static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(30);

    public static async Task<ApplicationResult> RunAsync(HiddenHostForm host, string[] args)
    {
        var paths = AppPaths.Initialize();

        if (!CliOptions.TryParse(args, out var options, out var cliError))
        {
            Logger.Warning(paths.LogFilePath, cliError);
            return new ApplicationResult { ExitCode = 2 };
        }

        if (!UrlValidator.TryValidate(options.Url, out var uri, out var urlError))
        {
            Logger.Warning(paths.LogFilePath, urlError + " Input=" + options.Url);
            return new ApplicationResult { ExitCode = 3 };
        }

        var config = ConfigLoader.LoadOrCreate(paths.ConfigPath, paths.LogFilePath);
        var safeUrl = UrlValidator.SafeDisplayUrl(uri);
        Logger.Info(paths.LogFilePath, $"Starting extraction. Mode={options.Mode}; URL={safeUrl}; AIEnabled={config.Enabled}; Model={config.Model}.");

        var tempProfile = Path.Combine(AppContext.BaseDirectory, "EnvironmentWebview", "WebToMarkdown_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempProfile);
            await host.Browser.InitializeAsync(tempProfile, paths.LogFilePath).ConfigureAwait(true);

            using var cts = new CancellationTokenSource(PageTimeout + TimeSpan.FromSeconds(5));
            var rule = SiteRules.ForHost(uri.Host);
            var page = await host.Browser.ExtractAsync(uri, rule, PageTimeout, paths.LogFilePath, cts.Token).ConfigureAwait(true);

            string content;
            if (options.Mode == ExtractionMode.Keyword || options.Mode == ExtractionMode.KeywordStat)
            {
                var analysis = KeywordAnalyzer.Analyze(page.PlainText);
                content = options.Mode == ExtractionMode.Keyword
                    ? KeywordAnalyzer.BuildKeywordReport(analysis)
                    : KeywordAnalyzer.BuildStatisticsReport(analysis);
            }
            else if (options.Mode == ExtractionMode.Resume)
            {
                var ai = new AiSummaryService(config, paths.LogFilePath);
                var aiSummary = await ai.TrySummarizeAsync(page, cts.Token).ConfigureAwait(true);
                var usedAi = !string.IsNullOrWhiteSpace(aiSummary);
                var summary = usedAi ? aiSummary! : LocalExtractiveSummarizer.Summarize(page.PlainText);
                if (!usedAi)
                    Logger.Info(paths.LogFilePath, "AI summary unavailable; local extractive fallback used.");
                content = MarkdownReportBuilder.BuildResume(page, summary, usedAi, uri);
            }
            else
            {
                content = MarkdownReportBuilder.BuildFull(page, uri);
            }

            var fileName = FileNameBuilder.Build(options.Mode, page, uri);
            var outputPath = Path.Combine(paths.ReportsDirectory, fileName);
            await AtomicWriteAsync(outputPath, content).ConfigureAwait(true);
            Logger.Info(paths.LogFilePath, "Report written: " + outputPath);

            return new ApplicationResult
            {
                ExitCode = 0,
                OutputPath = outputPath
            };
        }
        catch (Exception ex)
        {
            Logger.Error(paths.LogFilePath, "Extraction failed for " + safeUrl, ex);
            return new ApplicationResult { ExitCode = 1 };
        }
        finally
        {
            try
            {
                host.Browser.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warning(paths.LogFilePath, "WebView2 disposal reported an error: " + ex.Message);
            }

            try
            {
                if (Directory.Exists(tempProfile))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "Purge.exe",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(paths.LogFilePath, "Temporary WebView2 profile cleanup failed: " + ex.Message);
            }
        }
    }

    static async Task AtomicWriteAsync(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, "." + Path.GetFileName(path) + ".tmp");
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        await File.WriteAllTextAsync(tempPath, content, utf8).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }
}
