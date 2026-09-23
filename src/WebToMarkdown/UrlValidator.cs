namespace WebToMarkdown;

internal static class UrlValidator
{
    public static bool TryValidate(string input, out Uri uri, out string error)
    {
        uri = null!;
        error = string.Empty;

        if (!Uri.TryCreate(input, UriKind.Absolute, out var parsed))
        {
            error = "Invalid URL.";
            return false;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            error = "Only HTTP and HTTPS URLs are supported.";
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            error = "URLs containing embedded credentials are not supported.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsed.Host))
        {
            error = "The URL has no valid host.";
            return false;
        }

        uri = parsed;
        return true;
    }

    public static string SafeDisplayUrl(Uri uri)
    {
        try
        {
            return uri.GetLeftPart(UriPartial.Path) + uri.Query;
        }
        catch
        {
            return uri.Host;
        }
    }
}
