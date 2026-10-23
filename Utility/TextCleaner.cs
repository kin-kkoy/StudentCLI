using System.Net;
using System.Text.RegularExpressions;

namespace CanvasMail.Utility;

public static class TextCleaner
{
    public static string CleanHtml(string? rawHTML)
    {
        if(string.IsNullOrWhiteSpace(rawHTML))  return string.Empty;

        string text = rawHTML;

        // 0. Remove blocks whose CONTENTS aren't meant to be read (step 2 only removes the tags, not what's between them)
        //      <style>...</style>   -> CSS rules (emails love these)
        //      <script>...</script> -> JavaScript
        //      <!-- ... -->         -> HTML comments (incl. Outlook's <!--[if mso]> ... <![endif]--> blocks)
        //    `.*?` = lazy: stop at the FIRST closing tag, not the last one in the whole email
        //    Singleline = let `.` also match newlines, since these blocks span many lines
        text = Regex.Replace(text, @"<(style|script)\b[^>]*>.*?</\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<!--.*?-->", string.Empty, RegexOptions.Singleline);

        // 1. Replace line break and paragraph tags with real terminal newlines
        text = Regex.Replace(text, @"<(br|p|div|li)[^>]*>", "\n", RegexOptions.IgnoreCase);

        // 2. Strip all remaining HTML tags (<anything>)
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);

        // 3. Decode HTML entities (&nbsp; -> ' ', &amp; -> '&', etc.)
        text = WebUtility.HtmlDecode(text);

        // 4. Normalize Windows line endings (\r\n) and trim spaces at the start/end of every line
        //    (leftover HTML indentation;   is the non-breaking space that &nbsp; decodes into)
        text = text.Replace("\r\n", "\n");
        text = Regex.Replace(text, @"^[ \t ]+|[ \t ]+$", string.Empty, RegexOptions.Multiline);

        // 5. Collapse 3+ consecutive newlines down to 2 so output isn't overly spaced
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }
}
