/*  [ ] Service Orchestration
        - [ ] Create an application controller / state manager to coordinate Canvas and Gmail services.
        - [ ] Opening an email auto-marks it as read (fetch content + MarkAsReadAsync).
        - [ ] Implement unified error handling and network fault tolerance (friendly error messages instead of crashes, request timeouts).

            - Rate-limit backoff dropped: a personal tool doesn't come close to Canvas/Gmail limits, and Google's library already retries 503s.
*/

using CanvasMail.Services.GmailClient;
using CanvasMail.Services.Canvas;
using Google.Apis.Auth.OAuth2;
using CanvasMail.Models;

namespace CanvasMail.Controllers;

public class HubController
{
    private readonly CanvasService _canvas;
    private GmailClientService _gmail;              // not readonly: replaced when signing in to Google again
    private readonly ClientSecrets _googleSecrets;  // kept so we CAN sign in again without restarting

    // For dashboard items in Canvas
    private List<PlannerItem> _dashboardFeed = [];

    // Courses created on/after this date count as "this semester" (changeable in the app's Settings)
    public DateTime SemesterStart { get; set; } = new DateTime(2026, 08, 01);


    // Constructor part 2
    private HubController(CanvasService canvas, GmailClientService gmail, ClientSecrets googleSecrets)
    {
        _canvas = canvas;
        _gmail = gmail;
        _googleSecrets = googleSecrets;
    }

    // Constructor part 1 (used to create connection)
    public static async Task<HubController> CreateAsync(string canvasDomain, string canvasToken, ClientSecrets googleSecrets)
    {
        var canvasConnection = new CanvasService(canvasDomain, canvasToken);
        var gmailConnection = await GmailClientService.ConnectAsync(googleSecrets);

        return new HubController(canvasConnection, gmailConnection, googleSecrets);
    }

    // Saved Google login stopped working -> delete it, sign in again (opens the browser), swap in the new connection
    public async Task ReconnectGmailAsync()
    {
        await GmailClientService.ForgetSavedLoginAsync();
        _gmail = await GmailClientService.ConnectAsync(_googleSecrets);
    }


    // ==== CANVAS ================================================
    public async Task<IReadOnlyList<PlannerItem>> GetDashboardAsync()
    {
        _dashboardFeed = await _canvas.FetchDashboardFeedAsync();
        return _dashboardFeed; // for viewing purposes, no editting.
    }

    public Task<List<Course>> GetCurrentCoursesAsync() =>
        _canvas.FetchCurrentSemesterCoursesAsync(SemesterStart);

    public Task<List<CourseAnnouncement>> GetCourseAnnouncementsAsync(string courseID) =>
        _canvas.FetchCourseAnnouncementsAsync(courseID);
    
    public Task<AnnouncementDetail?> GetCourseAnnouncementAsync(string courseID, long announcementID) =>
        _canvas.FetchAnnouncementAsync(courseID, announcementID);

    public Task<Assignment?> GetAssignmentAsync(string courseID, long assignmentID) =>
        _canvas.FetchAssignmentAsync(courseID, assignmentID);

    public async Task<CompleteTodoResult> CompleteTodoAsync(long itemID)
    {
        foreach(var item in _dashboardFeed)
        {
            if(item.PlannableID == itemID)
            {
                bool isSuccess = await _canvas.MarkItemCompleteAsync(item);

                return isSuccess
                    ? CompleteTodoResult.Completed
                    : CompleteTodoResult.Failed;
            }
        }

        // if not found in the foreach loop = doesn't exist
        return CompleteTodoResult.NotFound;
    }


    // ==== GMAIL ================================================
    public Task<List<EmailSummary>> GetUnreadEmailsAsync() =>
        _gmail.FetchUnreadEmailsAsync();
    
    public async Task<EmailDetail> OpenEmailAsync(string emailID)
    {
        var email = await _gmail.FetchEmailContentAsync(emailID);
        await _gmail.MarkAsRead(emailID);

        return email;
    }

    public Task<bool> StarEmailAsync(string emailID) =>
        _gmail.StarEmail(emailID);

    public Task<bool> TrashEmailAsync(string emailID) =>
        _gmail.TrashEmailAsync(emailID);



    // ==== HELPER ================================================
    public async Task<MorningBriefing> GetMorningBriefingAsync()
    {
        var todosTask = CaptureAsync(GetDashboardAsync());
        var emailsTask = CaptureAsync(GetUnreadEmailsAsync());

        await Task.WhenAll(todosTask, emailsTask);

        var (Todos, TodosError) = await todosTask;
        var (Emails, EmailsError) = await emailsTask;

        return new MorningBriefing(Todos, TodosError, Emails, EmailsError);
    }
    
    static async Task<(T? value, Exception? Error)> CaptureAsync<T>(Task<T> task)
    {
        try
        {
            return (await task, null);
        }
        catch (Exception e)
        {
            return (default, e);
        }
    }

}
