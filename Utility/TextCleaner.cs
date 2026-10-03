using System.Net;
using System.Text.RegularExpressions;

namespace CanvasMail.Utility;

public static class TextCleaner
{
    public static string CleanHtml(string? rawHTML)
    {
        if(string.IsNullOrWhiteSpace(rawHTML))  return string.Empty;

        string text = rawHTML;

        // 1. Replace line break and paragraph tags with real terminal newlines        
        text = Regex.Replace(text, @"<(br|p|div|li)[^>]*>", "\n", RegexOptions.IgnoreCase);

        // 2. Strip all remaining HTML tags (<anything>)
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        
        // 3. Decode HTML entities (&nbsp; -> ' ', &amp; -> '&', etc.)
        text = WebUtility.HtmlDecode(text);

        // 4. Collapse 3+ consecutive newlines down to 2 so output isn't overly spaced
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }
}