using Google.Apis.Auth.OAuth2;

using CanvasMail.Controllers;
using CanvasMail.UI;


// Env variables ==================================
// Looks in ~/.config/TerminalHub/.env first, so the installed `hub` command works from ANY folder.
//  Falls back to ./.env (the project folder) for `dotnet run` while developing.
// (DoNotVerify = give the path even if ~/.config doesn't exist yet, instead of an empty string)
string configEnvFile = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
    "TerminalHub", ".env");
DotNetEnv.Env.Load(File.Exists(configEnvFile) ? configEnvFile : ".env");

//  GMAIL =======
var googleClientSecrets = new ClientSecrets
{
    ClientId = RequireEnv("GOOGLE_CLIENT_ID"),
    ClientSecret = RequireEnv("GOOGLE_CLIENT_SECRET")
};
//  CANVAS =======
string domain = RequireEnv("CANVAS_DOMAIN");
string accessToken = RequireEnv("CANVAS_TOKEN");


// INITIALIZE ==================================
// (Before the full-screen UI starts, so a Google login prompt / browser tab is still visible if the token expired)
Console.WriteLine("Connecting to Canvas & Gmail...");
var hub = await HubController.CreateAsync(domain, accessToken, googleClientSecrets);


// UI ==================================
await new App(hub).RunAsync();


// Startup config check: a clear message instead of a crash + stack trace
string RequireEnv(string name)
{
    string? value = Environment.GetEnvironmentVariable(name);
    if (!string.IsNullOrWhiteSpace(value)) return value;

    Console.Error.WriteLine($"Missing {name}.");
    Console.Error.WriteLine($"Add it to {configEnvFile}");
    Console.Error.WriteLine("(needs CANVAS_DOMAIN, CANVAS_TOKEN, GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET)");
    Environment.Exit(1);
    return "";  // never reached; Exit ends the program
}
