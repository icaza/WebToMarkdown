using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WebToMarkdown;

internal sealed class WebView2Host : WebView2
{
    private TaskCompletionSource<bool>? _navigationTcs;
    private bool _initialized;
    private int _navigationGeneration;

    public async Task InitializeAsync(string userDataFolder, string logPath)
    {
        if (_initialized)
            return;

        var options = new CoreWebView2EnvironmentOptions
        {
            AreBrowserExtensionsEnabled = false,
            AllowSingleSignOnUsingOSPrimaryAccount = false
        };

        var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options).ConfigureAwait(true);
        await EnsureCoreWebView2Async(environment).ConfigureAwait(true);

        var settings = CoreWebView2.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsScriptEnabled = true;

        CoreWebView2.NewWindowRequested += OnNewWindowRequested;
        CoreWebView2.NavigationStarting += OnNavigationStarting;
        CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        CoreWebView2.ProcessFailed += OnProcessFailed;
        CoreWebView2.DownloadStarting += OnDownloadStarting;
        CoreWebView2.PermissionRequested += OnPermissionRequested;

        _initialized = true;
        Logger.Info(logPath, "WebView2 initialized. Runtime=" + CoreWebView2Environment.GetAvailableBrowserVersionString());
    }

    public async Task<ExtractionResult> ExtractAsync(Uri uri, SiteRule rule, TimeSpan timeout, string logPath, CancellationToken cancellationToken)
    {
        if (!_initialized)
            throw new InvalidOperationException("WebView2 has not been initialized.");

        var generation = Interlocked.Increment(ref _navigationGeneration);
        var navigationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _navigationTcs = navigationTcs;

        var navigationUri = uri.AbsoluteUri;
        CoreWebView2.Navigate(navigationUri);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var registration = timeoutCts.Token.Register(() => navigationTcs.TrySetCanceled(timeoutCts.Token));
            await navigationTcs.Task.ConfigureAwait(true);

            // Give client-rendered pages a brief opportunity to finish hydrating after NavigationCompleted.
            await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(true);

            var ruleJson = JsonSerializer.Serialize(rule);
            var script = ExtractionScriptBuilder.Build(ruleJson);
            var raw = await CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("The page returned no extractable DOM data.");

            var result = JsonSerializer.Deserialize<ExtractionResult>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result is null)
                throw new InvalidOperationException("The page extraction result could not be decoded.");

            result.Host = uri.Host;
            Logger.Info(logPath, $"Page extracted. Generation={generation}; SiteRule={result.UsedSiteRule}; Score={result.ExtractionScore:0.00}; Paragraphs={result.ParagraphCount}; Characters={result.PlainText.Length}.");
            return result;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Navigation or extraction timed out after {timeout.TotalSeconds:0} seconds.");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            e.Cancel = true;
            return;
        }

        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _navigationTcs?.TrySetResult(true);
            return;
        }

        _navigationTcs?.TrySetException(new InvalidOperationException($"Web navigation failed: {e.WebErrorStatus}."));
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        _navigationTcs?.TrySetException(new InvalidOperationException("WebView2 process failure: " + e.ProcessFailedKind));
    }

    private static void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;
    }

    private static void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.State = CoreWebView2PermissionState.Deny;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && CoreWebView2 is not null)
        {
            CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
            CoreWebView2.NavigationStarting -= OnNavigationStarting;
            CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            CoreWebView2.ProcessFailed -= OnProcessFailed;
            CoreWebView2.DownloadStarting -= OnDownloadStarting;
            CoreWebView2.PermissionRequested -= OnPermissionRequested;
        }

        base.Dispose(disposing);
    }
}

internal static class ExtractionScriptBuilder
{
    public static string Build(string ruleJson)
    {
        return "(() => {" + Script + "\nconst cfg = " + ruleJson + ";\nreturn extract(cfg);\n})()";
    }

    private const string Script = """
function cleanText(value) {
  return String(value || '')
    .replace(/\u00a0/g, ' ')
    .replace(/[ \t]+/g, ' ')
    .replace(/\s*\n\s*/g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

function isVisible(el) {
  if (!el || !(el instanceof Element)) return false;
  const s = getComputedStyle(el);
  if (s.display === 'none' || s.visibility === 'hidden' || s.visibility === 'collapse') return false;
  if (s.opacity === '0') return false;
  if (el.hasAttribute('hidden') || el.getAttribute('aria-hidden') === 'true') return false;
  return true;
}

function isBoilerplate(el) {
  const value = ((el.id || '') + ' ' + (el.className || '') + ' ' + (el.getAttribute('role') || '')).toLowerCase();
  return /(cookie|consent|gdpr|paywall|newsletter|subscribe|subscription|advert|advertisement|banner|modal|popup|social-share|share-tools|breadcrumb|related-post|recommendation|login|signup|sign-in|register|toolbar|pagination|sidebar|comments?)/.test(value);
}

function normalizeLink(url) {
  try { return new URL(url, location.href).href; } catch { return ''; }
}

function inlineText(el) {
  return cleanText(el.innerText || el.textContent || '');
}

function inlineMarkdown(el) {
  if (!el) return '';
  const clone = el.cloneNode(true);
  clone.querySelectorAll('script,style,noscript,template').forEach(x => x.remove());
  return cleanText(clone.innerText || clone.textContent || '');
}

function textForLeaf(el) {
  if (!isVisible(el)) return '';
  return inlineMarkdown(el);
}

function escapeTableCell(v) {
  return cleanText(v).replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
}

function markdownInline(el) {
  const walk = (node) => {
    if (node.nodeType === Node.TEXT_NODE) return node.nodeValue || '';
    if (node.nodeType !== Node.ELEMENT_NODE) return '';
    if (!isVisible(node)) return '';
    const tag = node.tagName.toLowerCase();
    if (['script','style','noscript','template','svg','canvas'].includes(tag)) return '';
    if (tag === 'br') return '\n';
    if (tag === 'a') {
      const txt = cleanText(Array.from(node.childNodes).map(walk).join(''));
      const href = normalizeLink(node.getAttribute('href') || '');
      return txt && href ? '[' + txt + '](' + href.replace(/\)/g, '%29') + ')' : txt;
    }
    if (tag === 'strong' || tag === 'b') {
      const txt = cleanText(Array.from(node.childNodes).map(walk).join(''));
      return txt ? '**' + txt + '**' : '';
    }
    if (tag === 'em' || tag === 'i') {
      const txt = cleanText(Array.from(node.childNodes).map(walk).join(''));
      return txt ? '*' + txt + '*' : '';
    }
    if (tag === 'del' || tag === 's') {
      const txt = cleanText(Array.from(node.childNodes).map(walk).join(''));
      return txt ? '~~' + txt + '~~' : '';
    }
    if (tag === 'code') return '`' + (node.textContent || '').replace(/`/g, '\\`') + '`';
    return Array.from(node.childNodes).map(walk).join('');
  };
  return cleanText(Array.from(el.childNodes).map(walk).join(''));
}

function directBlockChildren(el) {
  return Array.from(el.children).filter(isVisible);
}

function renderElement(el, depth) {
  if (!isVisible(el) || depth > 20) return [];
  const tag = el.tagName.toLowerCase();
  if (isBoilerplate(el)) return [];

  if (/^h[1-6]$/.test(tag)) {
    const level = Number(tag.substring(1));
    const text = markdownInline(el);
    return text ? ['#'.repeat(level) + ' ' + text, ''] : [];
  }

  if (tag === 'p') {
    const text = markdownInline(el);
    return text ? [text, ''] : [];
  }

  if (tag === 'blockquote') {
    const text = cleanText(el.innerText || el.textContent || '');
    return text ? [text.split('\n').map(x => '> ' + cleanText(x)).join('\n'), ''] : [];
  }

  if (tag === 'pre') {
    const code = el.textContent || '';
    if (!code.trim()) return [];
    return ['```', code.replace(/\r\n/g, '\n').replace(/\r/g, '\n').trimEnd(), '```', ''];
  }

  if (tag === 'ul' || tag === 'ol') {
    const ordered = tag === 'ol';
    const lines = [];
    let index = 1;
    el.querySelectorAll(':scope > li').forEach(li => {
      if (!isVisible(li) || isBoilerplate(li)) return;
      const clone = li.cloneNode(true);
      clone.querySelectorAll(':scope > ul, :scope > ol').forEach(x => x.remove());
      const text = markdownInline(clone);
      if (text) lines.push((ordered ? (index++) + '. ' : '- ') + text);
    });
    return lines.length ? [...lines, ''] : [];
  }

  if (tag === 'hr') return ['---', ''];

  if (tag === 'table') {
    const rows = Array.from(el.querySelectorAll('tr')).filter(isVisible);
    const matrix = rows.map(row => Array.from(row.children).map(cell => escapeTableCell(cell.innerText || cell.textContent || '')));
    if (!matrix.length) return [];
    const width = Math.max(...matrix.map(r => r.length));
    const normalized = matrix.map(r => { const x = r.slice(); while (x.length < width) x.push(''); return x; });
    if (width === 0) return [];
    const header = normalized[0];
    const lines = ['| ' + header.join(' | ') + ' |', '| ' + header.map(() => '---').join(' | ') + ' |'];
    for (let i = 1; i < normalized.length; i++) lines.push('| ' + normalized[i].join(' | ') + ' |');
    return [...lines, ''];
  }

  if (tag === 'figure') {
    const caption = el.querySelector('figcaption');
    const img = el.querySelector('img');
    const lines = [];
    if (img) {
      const src = normalizeLink(img.getAttribute('src') || img.getAttribute('data-src') || '');
      const alt = cleanText(img.getAttribute('alt') || '');
      if (src) lines.push('![' + alt.replace(/\]/g, '\\]') + '](' + src.replace(/\)/g, '%29') + ')');
    }
    if (caption) {
      const text = cleanText(caption.innerText || caption.textContent || '');
      if (text) lines.push('*' + text + '*');
    }
    return lines.length ? [...lines, ''] : [];
  }

  if (tag === 'dl') {
    const lines = [];
    Array.from(el.children).forEach(child => {
      const ct = child.tagName.toLowerCase();
      if (ct === 'dt') {
        const text = markdownInline(child);
        if (text) lines.push('**' + text + '**');
      } else if (ct === 'dd') {
        const text = markdownInline(child);
        if (text) lines.push(': ' + text);
      }
    });
    return lines.length ? [...lines, ''] : [];
  }

  const children = directBlockChildren(el);
  const blockTags = new Set(['article','main','section','div','body','header','footer','aside','nav','li','td','dd','dt']);
  if (blockTags.has(tag) && children.length) {
    const out = [];
    children.forEach(child => out.push(...renderElement(child, depth + 1)));
    return out;
  }

  const text = markdownInline(el);
  if (text.length >= 40) return [text, ''];
  return [];
}

function candidateScore(el) {
  const text = cleanText(el.innerText || '');
  if (text.length < 120) return -Infinity;
  const paragraphs = el.querySelectorAll('p').length;
  const links = Array.from(el.querySelectorAll('a')).reduce((n, a) => n + (a.innerText || '').length, 0);
  const density = text.length ? Math.min(1, links / text.length) : 0;
  const attr = ((el.id || '') + ' ' + (el.className || '')).toLowerCase();
  let boost = 0;
  if (/(article|content|entry|post|story|body|main|readme|markdown)/.test(attr)) boost += 900;
  if (/(comment|sidebar|nav|menu|footer|header|related|recommend)/.test(attr)) boost -= 900;
  return Math.log10(text.length) * 120 + Math.min(paragraphs, 80) * 28 - density * 1100 + boost;
}

function firstMeta(selectors) {
  for (const selector of selectors) {
    try {
      const el = document.querySelector(selector);
      if (el) return cleanText(el.getAttribute('content') || el.innerText || '');
    } catch {}
  }
  return '';
}

function extract(config) {
  const removeSelectors = [
    'script','style','noscript','template','svg','canvas','iframe','object','embed',
    'nav','footer','form',
    '[aria-hidden="true"]','[hidden]',
    '.cookie','.cookies','.consent','.privacy','.newsletter','.paywall','.modal','.popup',
    '.advert','.advertisement','.ads','.ad','.social-share','.share-tools','.breadcrumbs',
    '.breadcrumb','.pagination','.related','.recommendations'
  ];

  removeSelectors.forEach(selector => document.querySelectorAll(selector).forEach(el => el.remove()));
  (config.ExcludeSelectors || []).forEach(selector => { try { document.querySelectorAll(selector).forEach(el => el.remove()); } catch {} });

  const candidates = [];
  (config.ContentSelectors || []).forEach(selector => {
    try { document.querySelectorAll(selector).forEach(el => candidates.push({el, rule:true})); } catch {}
  });

  ['article','main','[role="main"]','[itemprop="articleBody"]','[class*="article-body"]','[class*="articleBody"]','[class*="post-content"]','[class*="entry-content"]','[class*="markdown-body"]'].forEach(selector => {
    try { document.querySelectorAll(selector).forEach(el => candidates.push({el, rule:false})); } catch {}
  });

  if (document.body) candidates.push({el: document.body, rule:false});

  const unique = [];
  const seen = new Set();
  for (const item of candidates) {
    if (!item.el || seen.has(item.el)) continue;
    seen.add(item.el);
    unique.push(item);
  }

  let best = null;
  const ruleCandidates = unique.filter(x => x.rule && candidateScore(x.el) > -Infinity);
  const pool = ruleCandidates.length ? ruleCandidates : unique;
  for (const candidate of pool) {
    const score = candidateScore(candidate.el) + (candidate.rule ? 2500 : 0);
    if (!best || score > best.score) best = { ...candidate, score };
  }

  if (!best) throw new Error('No suitable content container was found.');

  const lines = renderElement(best.el, 0)
    .join('\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();

  const plain = cleanText(best.el.innerText || best.el.textContent || '');
  const title = cleanText(document.querySelector('h1')?.innerText || document.title || '') || 'Web page';
  const description = firstMeta(['meta[name="description"]','meta[property="og:description"]']);
  const author = firstMeta(['meta[name="author"]','meta[property="article:author"]','[rel="author"]']);
  const published = firstMeta(['meta[property="article:published_time"]','meta[name="date"]','time[datetime]']);
  const canonical = document.querySelector('link[rel="canonical"]')?.href || location.href;

  return {
    Title: title,
    Description: description,
    Author: author,
    Published: published,
    CanonicalUrl: canonical,
    PlainText: plain,
    Markdown: lines || plain,
    Host: location.host,
    ParagraphCount: best.el.querySelectorAll('p').length,
    UsedSiteRule: Boolean(best.rule),
    ExtractionScore: best.score
  };
}
""";
}
