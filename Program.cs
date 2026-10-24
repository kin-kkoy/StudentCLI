using Google.Apis.Auth.OAuth2;

using CanvasMail.Controllers;
using CanvasMail.UI;


// Env variables ==================================
DotNetEnv.Env.Load();
//  GMAIL =======
var googleClientSecrets = new ClientSecrets
{
    ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not found in .env"),
    ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not found in .env")
};
//  CANVAS =======
string domain = Environment.GetEnvironmentVariable("CANVAS_DOMAIN")
    ?? throw new InvalidOperationException("CANVAS_DOMAIN not found in .env");
string accessToken = Environment.GetEnvironmentVariable("CANVAS_TOKEN")
    ?? throw new InvalidOperationException("CANVAS_TOKEN not found in .env");


// INITIALIZE ==================================
// (Before the full-screen UI starts, so a Google login prompt / browser tab is still visible if the token expired)
Console.WriteLine("Connecting to Canvas & Gmail...");
var hub = await HubController.CreateAsync(domain, accessToken, googleClientSecrets);


// UI ==================================
await new App(hub).RunAsync();
