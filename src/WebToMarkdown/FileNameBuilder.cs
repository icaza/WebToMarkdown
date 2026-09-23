using System.Security.Cryptography;
using System.Text;

namespace WebToMarkdown;

internal static class FileNameBuilder
{
    public static string Build(ExtractionMode mode, ExtractionResult page, Uri source)
    {
        var prefix = mode switch
        {
            ExtractionMode.KeywordStat => "keywordstat",
            ExtractionMode.Keyword => "keyword",
            ExtractionMode.Resume => "resume",
            _ => "page"
        };

        var title = string.IsNullOrWhiteSpace(page.Title) ? "web-page" : page.Title;
        var slug = Sanitize(title);
        var host = Sanitize(source.Host);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var hash = ShortHash(source.AbsoluteUri);
        var name = $"{prefix}_{host}_{slug}_{stamp}_{hash}.md";
        return name.Length <= 180 ? name : name[..174] + "_" + hash + ".md";
    }

    static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Normalize(NormalizationForm.FormC))
        {
            if (Array.IndexOf(invalid, c) >= 0 || char.IsControl(c))
                sb.Append('-');
            else if (char.IsWhiteSpace(c))
                sb.Append('-');
            else
                sb.Append(c);
        }

        var result = sb.ToString().Trim('-', '.', ' ');
        while (result.Contains("--", StringComparison.Ordinal))
            result = result.Replace("--", "-");
        return string.IsNullOrWhiteSpace(result) ? "web-page" : result;
    }

    static string ShortHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..10].ToLowerInvariant();
    }
}
