using System.Text;
using System.Text.RegularExpressions;

namespace WebToMarkdown;

internal static partial class LocalExtractiveSummarizer
{
    [GeneratedRegex(@"[\p{L}\p{M}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    public static string Summarize(string text, int maxSentences = 7)
    {
        var sentences = SplitSentences(text)
            .Select(NormalizeSentence)
            .Where(s => s.Length >= 35)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (sentences.Count == 0)
            return text.Trim();

        if (sentences.Count <= maxSentences)
            return string.Join(" ", sentences);

        var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var sentence in sentences)
        {
            foreach (Match match in WordRegex().Matches(sentence))
            {
                var token = match.Value.ToLowerInvariant();
                if (token.Length <= 1)
                    continue;
                frequencies[token] = frequencies.TryGetValue(token, out var count) ? count + 1 : 1;
            }
        }

        var ranked = sentences
            .Select((sentence, index) => new
            {
                sentence,
                index,
                score = Score(sentence, frequencies)
            })
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Take(maxSentences)
            .OrderBy(x => x.index)
            .Select(x => x.sentence)
            .ToList();

        return string.Join("\n\n", ranked);
    }

    static double Score(string sentence, IReadOnlyDictionary<string, int> frequencies)
    {
        var words = WordRegex().Matches(sentence).Select(m => m.Value.ToLowerInvariant()).Where(x => x.Length > 1).ToList();
        if (words.Count == 0)
            return 0;

        var sum = words.Sum(word => frequencies.TryGetValue(word, out var count) ? Math.Log(1 + count) : 0);
        var lengthFactor = Math.Min(1.5, words.Count / 18d);
        var positionBonus = sentence.Length < 160 ? 1.0 : 0.85;
        return (sum / words.Count) * (0.7 + lengthFactor) * positionBonus;
    }

    static List<string> SplitSentences(string text)
    {
        var result = new List<string>();
        var sb = new StringBuilder();

        foreach (var rune in text.EnumerateRunes())
        {
            sb.Append(rune.ToString());
            var value = rune.Value;
            if (value is '.' or '!' or '?' or '。' or '！' or '？' or '۔')
            {
                AddSentence(sb, result);
            }
        }

        AddSentence(sb, result);
        return result;
    }

    static void AddSentence(StringBuilder sb, List<string> result)
    {
        var value = sb.ToString().Trim();
        if (value.Length > 0)
            result.Add(value);
        sb.Clear();
    }

    private static string NormalizeSentence(string sentence) =>
        Regex.Replace(sentence, @"\s+", " ").Trim();
}
