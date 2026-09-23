using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WebToMarkdown;

internal sealed record KeywordStat(string Word, int Count, double FrequencyPercent);

internal sealed class KeywordAnalysis
{
    public int TotalWords { get; init; }
    public int UniqueWords { get; init; }
    public IReadOnlyList<string> Words { get; init; } = Array.Empty<string>();
    public IReadOnlyList<KeywordStat> Statistics { get; init; } = Array.Empty<KeywordStat>();
}

internal static partial class KeywordAnalyzer
{
    [GeneratedRegex(@"[\p{L}\p{M}\p{N}]+(?:['’_-][\p{L}\p{M}\p{N}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    public static KeywordAnalysis Analyze(string text)
    {
        var words = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TokenRegex().Matches(text))
        {
            var token = NormalizeToken(match.Value);
            if (token.Length == 0)
                continue;

            // CJK/Hangul scripts commonly do not use spaces between words.
            // For those scripts, individual letters/ideographs are counted as tokens.
            if (ContainsUnsegmentedScript(token))
            {
                foreach (var rune in token.EnumerateRunes())
                {
                    if (IsCjkLike(rune.Value))
                    {
                        var part = rune.ToString();
                        words.Add(part);
                        counts[part] = counts.TryGetValue(part, out var value) ? value + 1 : 1;
                    }
                }

                continue;
            }

            words.Add(token);
            counts[token] = counts.TryGetValue(token, out var existing) ? existing + 1 : 1;
        }

        var total = words.Count;
        var stats = counts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new KeywordStat(x.Key, x.Value, total == 0 ? 0 : x.Value * 100d / total))
            .ToList();

        return new KeywordAnalysis
        {
            TotalWords = total,
            UniqueWords = counts.Count,
            Words = words,
            Statistics = stats
        };
    }

    public static string BuildKeywordReport(KeywordAnalysis analysis)
    {
        var sb = new StringBuilder();
        foreach (var word in analysis.Words)
            sb.AppendLine(word);
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public static string BuildStatisticsReport(KeywordAnalysis analysis)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Keyword Statistics");
        sb.AppendLine();
        sb.AppendLine($"- Total words: **{analysis.TotalWords:N0}**");
        sb.AppendLine($"- Unique words: **{analysis.UniqueWords:N0}**");
        sb.AppendLine();
        sb.AppendLine("## Most Used Words");
        sb.AppendLine();
        sb.AppendLine("| Rank | Word | Count | Frequency |");
        sb.AppendLine("| ---: | --- | ---: | ---: |");

        var rank = 1;
        foreach (var item in analysis.Statistics.Take(100))
        {
            sb.Append("| ").Append(rank++).Append(" | ")
                .Append(EscapeTable(item.Word))
                .Append(" | ").Append(item.Count.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" | ").Append(item.FrequencyPercent.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine("% |");
        }

        if (analysis.Statistics.Count == 0)
        {
            sb.AppendLine("| - | No words found | 0 | 0.00% |");
        }

        return sb.ToString();
    }

    static string NormalizeToken(string token)
    {
        var value = token.Normalize(NormalizationForm.FormC).Trim(' ', '\t', '\r', '\n', '\'', '’', '-', '_');
        return value.ToLowerInvariant();
    }

    static bool ContainsUnsegmentedScript(string token) => token.EnumerateRunes().Any(r => IsCjkLike(r.Value));

    static bool IsCjkLike(int value) =>
        (value is >= 0x3400 and <= 0x4DBF) ||
        (value is >= 0x4E00 and <= 0x9FFF) ||
        (value is >= 0xF900 and <= 0xFAFF) ||
        (value is >= 0x3040 and <= 0x30FF) ||
        (value is >= 0xAC00 and <= 0xD7AF);

    static string EscapeTable(string value) => value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
