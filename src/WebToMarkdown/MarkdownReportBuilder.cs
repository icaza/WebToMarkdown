using System.Text;

namespace WebToMarkdown;

internal static class MarkdownReportBuilder
{
    public static string BuildFull(ExtractionResult page, Uri source)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# " + EscapeHeading(page.Title));
        sb.AppendLine();
        sb.AppendLine("> Source: " + UrlValidator.SafeDisplayUrl(source));
        if (!string.IsNullOrWhiteSpace(page.Author)) sb.AppendLine("> Author: " + CleanMeta(page.Author));
        if (!string.IsNullOrWhiteSpace(page.Published)) sb.AppendLine("> Published: " + CleanMeta(page.Published));
        if (!string.IsNullOrWhiteSpace(page.Description)) sb.AppendLine("> Description: " + CleanMeta(page.Description));
        sb.AppendLine("> Extracted: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        sb.AppendLine();
        sb.AppendLine(page.Markdown.Trim());
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public static string BuildResume(ExtractionResult page, string summary, bool usedAi, Uri source)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Summary");
        sb.AppendLine();
        sb.AppendLine(summary.Trim());
        sb.AppendLine();
        sb.AppendLine("## Source");
        sb.AppendLine();
        sb.AppendLine("> " + UrlValidator.SafeDisplayUrl(source));
        sb.AppendLine("> Summary mode: " + (usedAi ? "AI endpoint" : "Local extractive fallback"));
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    static string EscapeHeading(string value) => (string.IsNullOrWhiteSpace(value) ? "Web page" : value).Replace("\r", " ").Replace("\n", " ").Replace("#", "\\#").Trim();
    static string CleanMeta(string value) => value.Replace("\r", " ").Replace("\n", " ").Trim();
}
