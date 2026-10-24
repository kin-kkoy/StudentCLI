using System.Text.Json;

namespace CanvasMail.UI;


/*  User settings changed from inside the app (Settings screen), saved to
    ~/.config/TerminalHub/settings.json so they survive restarts without rebuilding.
*/
public sealed record AppSettings(DateTime SemesterStart)
{
    public static readonly AppSettings Default = new(new DateTime(2026, 08, 01));

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        "TerminalHub", "settings.json");


    public static AppSettings Load()
    {
        try
        {
            return File.Exists(SettingsFile)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? Default
                : Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Default;     // unreadable settings = start with the defaults rather than not start at all
        }
    }

    // Returns false if it couldn't be saved (the caller tells the user)
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
