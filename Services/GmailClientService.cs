using CanvasMail.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;


/*  ROADMAP (top to bottom, what this file will contain)

    PART 1 - using statements
      Gmail.v1       GmailService, Scope         [done]
      Auth.OAuth2    ClientSecrets,
                     UserCredential,
                     GoogleWebAuthorizationBroker
      Services       BaseClientService
      Util.Store     FileDataStore
      Gmail.v1.Data  Profile, Message (DTOs)
      (all start with "Google.Apis.")
      Add each one when you first need it.

    PART 2 - Settings (static readonly)
      Same for every object, never change.
      _scopes        what Gmail lets us do   [done] 2.2
      _tokenFolder   where token is saved    [done] 2.3

    PART 3 - The field (private readonly)    [next] 2.4
      _gmail         Google's GmailService
      Same job as _http in CanvasService:
      every request goes through it.

    PART 4 - Constructor (PRIVATE)           [next] 2.4
      Receives a GmailService,
      stores it in _gmail. Nothing else.
      Private, so Program.cs can't do
      `new GmailClientService(...)`.
      It must go through Part 5 instead.

    PART 5 - ConnectAsync                           2.5
      (public static async)
      The "front door" Program.cs calls.
      Not a constructor because logging in
      needs `await`, and constructors
      can't be async.
      Inside:
        a) AuthorizeAsync(...)
           -> UserCredential (logged-in pass)
           1st run: browser opens, click Allow
           Later:   reuses saved token
        b) new GmailService(...)
           -> give it the credential from (a)
        c) return new GmailClientService(b)
           -> this calls Part 4

    PART 6 - Action methods (public async)
      FetchProfileAsync()   login test      2.6
      FetchUnreadEmailsAsync(), ...    Phase 2
      Pattern:
        _gmail.Users.<Thing>("me")
              .ExecuteAsync()

    USED FROM Program.cs:
      1. var gmail = await
           GmailClientService.ConnectAsync(
               googleClientSecrets);  // Part 5
      2. var profile = await
           gmail.FetchProfileAsync(); // Part 6
      3. Console.WriteLine(
           profile.EmailAddress);
*/

namespace CanvasMail.Services.GmailClient;

public class GmailClientService
{
    private static readonly string[] _scopes = [GmailService.Scope.GmailModify];
    private static readonly string _tokenFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TerminalHub", "google-token");
    private readonly GmailService _gmail;


    private GmailClientService(GmailService gmail)
    {
        _gmail = gmail;
    }


    // METHODS ===========================================================================
    // Authentication ---
    public static async Task<GmailClientService> ConnectAsync(ClientSecrets secrets)
    {
        UserCredential userCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            _scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(_tokenFolder, true)
        );

        GmailService gmailService = new GmailService(
            new BaseClientService.Initializer
            {
                HttpClientInitializer = userCredential,
                ApplicationName = "TerminalHub"
            }
        );

        return new GmailClientService(gmailService);
    }

    public async Task<Profile> FetchProfileAsync() => await _gmail.Users.GetProfile("me").ExecuteAsync();


    // Content ---
    public async Task<List<EmailSummary>> FetchUnreadEmailsAsync(int maxResults = 8)
    {
        // List to be returned, whether null (empty) or not
        List<EmailSummary> unreadEmails = new List<EmailSummary>();

        // Format request
        var listRequest = _gmail.Users.Messages.List("me");
        listRequest.Q = "is:unread";
        listRequest.MaxResults = maxResults;
        // Send the request and get Response
        var listResponse = await listRequest.ExecuteAsync();
        if(listResponse.Messages is null)
            return unreadEmails;


        foreach (var item in listResponse.Messages)
        {
            // build req
            var messageReq = _gmail.Users.Messages.Get("me", item.Id);
            messageReq.Format  = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
        
            // send req, get response
            var messageRes = await messageReq.ExecuteAsync();

            // Formatting frking date
            var messageDate = messageRes.InternalDate.HasValue
                ?    DateTimeOffset.FromUnixTimeMilliseconds(messageRes.InternalDate.Value).LocalDateTime
                :   (DateTime?) null;


            // Add to list
            unreadEmails.Add(new EmailSummary(
                    ID: messageRes.Id,
                    From: GetHeader(messageRes, "From"),
                    Subject: GetHeader(messageRes, "Subject"),
                    Date: messageDate,
                    Snippet: messageRes.Snippet
                )
            );
        }

        return unreadEmails;
    }


    // HELPER METHODS ===========================================================================
    
    private static string GetHeader(Message message, string headerName)
    {
        foreach (var value in message.Payload.Headers)
        {
            if(string.Equals(value.Name, headerName, StringComparison.OrdinalIgnoreCase))
                return value.Value;
        }

        return "";
    }
}