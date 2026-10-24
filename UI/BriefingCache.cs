using System.Text.Json;

using CanvasMail.Models;

namespace CanvasMail.UI;


// The last briefing that loaded successfully, per side (a side is null if it never loaded)
public sealed record BriefingSnapshot(
    DateTime? TodosSavedAt, List<PlannerItem>? Todos,
    DateTime? EmailsSavedAt, List<EmailSummary>? Emails
);


/*  Saves the briefing to ~/.cache/TerminalHub/briefing.json so the next launch can show it
    INSTANTLY, while fresh data loads in the background ("stale-while-revalidate").

    It's only a convenience: if the file is missing, corrupt, or can't be written,
    the app just behaves as if there were no cache. It never crashes over it.
*/
public static class BriefingCache
{
    private static readonly string CacheFile = Path.Combine(CacheDirectory(), "briefing.json");


    public static BriefingSnapshot? Load()
    {
        try
        {
            return File.Exists(CacheFile)
                ? JsonSerializer.Deserialize<BriefingSnapshot>(File.ReadAllText(CacheFile))
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(BriefingSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);

            // Write to a temp file first, then swap it in, so a crash mid-write can't leave a half-written cache
            string tempFile = CacheFile + ".tmp";
            File.WriteAllText(tempFile, JsonSerializer.Serialize(snapshot));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(tempFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);   // email subjects: only you can read it
            File.Move(tempFile, CacheFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not worth interrupting the user over; next save will try again
        }
    }

    // Linux convention: $XDG_CACHE_HOME, or ~/.cache if it isn't set
    private static string CacheDirectory()
    {
        string? xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        string root = string.IsNullOrEmpty(xdgCache)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache")
            : xdgCache;
        return Path.Combine(root, "TerminalHub");
    }
}
