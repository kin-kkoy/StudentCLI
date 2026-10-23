using System.Buffers.Text;
using System.Text;
using CanvasMail.Models;
using CanvasMail.Utility;
using Google;
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
    public async Task<List<EmailSummary>> FetchUnreadEmailsAsync(int maxResults = 5)
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


            // Add to list
            unreadEmails.Add(new EmailSummary(
                    ID: messageRes.Id,
                    From: GetHeader(messageRes, "From"),
                    Subject: GetHeader(messageRes, "Subject"),
                    Date: FormatDate(messageRes.InternalDate),
                    Snippet: messageRes.Snippet
                )
            );
        }

        return unreadEmails;
    }

    public async Task<EmailDetail> FetchEmailContentAsync(string messageID)
    {
        // req
        var messageReq = _gmail.Users.Messages.Get("me", messageID);
        messageReq.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

        // res
        var messageRes = await messageReq.ExecuteAsync();


        // Flow:
        //      1. get plain data first and check if it isn't null. 
        //      2. If it is, check for html data then and if isn't null.
        //      3. If it is then there's no data so return it as "no data".

        string? plainData = FindBodyData(messageRes.Payload, "text/plain");
        string messageBody;

        if(plainData is not null)   // decode: if email has plain-text version only
        {
            messageBody = DecodeBase64Url(plainData);
        }
        else       // decode: if email has html version only
        {
            string? htmlData = FindBodyData(messageRes.Payload, "text/html");
            if(htmlData is not null)
            {
                messageBody = TextCleaner.CleanHtml(DecodeBase64Url(htmlData));
            }
            else       // No data
            {
                messageBody = "(no readable content)";
            }
        }
        

        return new EmailDetail(
            ID: messageRes.Id,
            From: GetHeader(messageRes, "From"),
            To: GetHeader(messageRes, "To"),
            Subject: GetHeader(messageRes, "Subject"),
            Date: FormatDate(messageRes.InternalDate),
            Body: messageBody
        );
    }

    // Interaction with Email (Mark as Read, Star, etc.) ---
    public  Task<bool> MarkAsRead(string messageID) =>
        ModifyLabelsAsync(messageID, [], ["UNREAD"]);

    public  Task<bool> StarEmail(string messageID) => 
        ModifyLabelsAsync(messageID, ["STARRED"], []);

    public async Task<bool> TrashEmailAsync(string messageID)
    {
        try
        {
            await _gmail.Users.Messages.Trash("me", messageID).ExecuteAsync();
            return true;
        }
        catch (GoogleApiException)
        {
            return false;
        }
    }

    // HELPER METHODS ===========================================================================
    
    private async Task<bool> ModifyLabelsAsync(string messageID, string[] addLabels, string[] removeLabels)
    {
        try
        {
            ModifyMessageRequest body = new ModifyMessageRequest();
            body.AddLabelIds = addLabels;
            body.RemoveLabelIds = removeLabels;

            await _gmail.Users.Messages.Modify(body, "me", messageID).ExecuteAsync();
            return true;
        }
        catch (GoogleApiException)
        {
            return false;
        }
    }

    private static string GetHeader(Message message, string headerName)
    {
        foreach (var value in message.Payload.Headers)
        {
            if(string.Equals(value.Name, headerName, StringComparison.OrdinalIgnoreCase))
                return value.Value;
        }

        return "";
    }

    private static string? FindBodyData(MessagePart messagePart, string mimeType)
    {
        if(messagePart.Body?.Data is not null && string.Equals(messagePart.MimeType, mimeType, StringComparison.OrdinalIgnoreCase))
        {
            return messagePart.Body?.Data;
        }
        else if(messagePart.Parts is not null)
        {
            foreach (var childPart in messagePart.Parts)
            {
                var bodyFound = FindBodyData(childPart, mimeType);
                if(bodyFound is not null)   return bodyFound;
            }
        }

        // if none at all then
        return null;
    }

    private static string DecodeBase64Url(string data)
    {
        var byteArray = Base64Url.DecodeFromChars(data.AsSpan());
        return Encoding.UTF8.GetString(byteArray);
    }

    private static DateTime? FormatDate(long? inputDate) => 
        inputDate.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(inputDate.Value).LocalDateTime
            : (DateTime?) null;

}