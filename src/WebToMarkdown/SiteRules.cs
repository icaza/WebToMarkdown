namespace WebToMarkdown;

internal static class SiteRules
{
    static readonly IReadOnlyDictionary<string, SiteRule> Rules = new Dictionary<string, SiteRule>(StringComparer.OrdinalIgnoreCase)
    {
        ["github.com"] = new SiteRule
        {
            ContentSelectors = [
                "article.markdown-body",
                ".markdown-body",
                "[data-testid='readme-container']",
                "article"
            ],
            ExcludeSelectors = [
                "nav", "header", "footer", "aside", ".BorderGrid", ".js-discussion", ".timeline-comment", ".octicon"
            ]
        },
        ["gitlab.com"] = new SiteRule
        {
            ContentSelectors = [
                ".md", ".wiki-page-content", ".blob-viewer", "article", "main"
            ],
            ExcludeSelectors = ["nav", "header", "footer", ".sidebar", ".right-sidebar", ".gl-alert"]
        },
        ["wikipedia.org"] = new SiteRule
        {
            ContentSelectors = [
                "#mw-content-text .mw-parser-output",
                "#mw-content-text",
                "main"
            ],
            ExcludeSelectors = [
                "#mw-navigation", "#siteSub", ".mw-editsection", ".navbox", ".metadata", ".reference", ".reflist", ".catlinks", ".mw-jump"
            ]
        },
        ["medium.com"] = new SiteRule
        {
            ContentSelectors = ["article", "main"],
            ExcludeSelectors = ["nav", "header", "footer", "aside"]
        },
        ["substack.com"] = new SiteRule
        {
            ContentSelectors = ["article", ".body.markup", ".available-content", "main"],
            ExcludeSelectors = ["nav", "header", "footer", ".subscription-widget-wrap", ".paywall"]
        },
        ["dev.to"] = new SiteRule
        {
            ContentSelectors = ["article", ".crayons-article__main", "main"],
            ExcludeSelectors = ["nav", "header", "footer", ".crayons-story__indention"]
        },
        ["stackoverflow.com"] = new SiteRule
        {
            ContentSelectors = ["#question", ".answer", "main"],
            ExcludeSelectors = ["nav", "header", "footer", ".js-post-menu", ".post-signature"]
        },
        ["stackexchange.com"] = new SiteRule
        {
            ContentSelectors = [".question", ".answer", "main"],
            ExcludeSelectors = ["nav", "header", "footer", ".js-post-menu", ".post-signature"]
        },
        ["developer.mozilla.org"] = new SiteRule
        {
            ContentSelectors = ["main", "article", ".main-page-content"],
            ExcludeSelectors = ["nav", "header", "footer", "aside", ".sidebar"]
        },
        ["reddit.com"] = new SiteRule
        {
            ContentSelectors = [
                "[data-test-id='post-content']",
                "[slot='text-body']",
                "shreddit-post",
                "main"
            ],
            ExcludeSelectors = ["nav", "header", "footer", "aside", "faceplate-overflow-menu"]
        },
        ["arxiv.org"] = new SiteRule
        {
            ContentSelectors = [".ltx_main", "main", "article"],
            ExcludeSelectors = ["nav", "header", "footer", ".ltx_bibliography", ".ltx_authors" ]
        }
    };

    public static SiteRule ForHost(string host)
    {
        var normalized = host.Trim('.').ToLowerInvariant();

        foreach (var pair in Rules)
        {
            if (normalized.Equals(pair.Key, StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("." + pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return new SiteRule();
    }
}
