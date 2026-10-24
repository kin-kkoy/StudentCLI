using Spectre.Console;
using Spectre.Console.Rendering;

using CanvasMail.Controllers;
using CanvasMail.Models;
using CanvasMail.Utility;

namespace CanvasMail.UI.Screens;


/*  The briefing (home) screen: Canvas to-dos + unread Gmail, side by side.
    On narrow terminals (< 100 columns) the two panels stack on top of each other instead.

    ┌ Canvas · To-do (1/6) ────┐┌ Gmail · Unread (5) ──────┐
    │ ▸ ASG Thesis appeal      ││   CISCO: USC Days 2026   │
    │     Today 11:59 PM · ... ││     Czachary · 7:00 AM   │
    └──────────────────────────┘└──────────────────────────┘
*/
public sealed class HomeScreen : IScreen
{
    private const int SideBySideMinWidth = 100;

    private readonly HubController _hub;
    private readonly SelectableList<PlannerItem> _todos;
    private readonly SelectableList<EmailSummary> _emails;
    private bool _mailFocused;
    private string? _todoError;
    private string? _mailError;
    private bool _loaded;

    public string Title => "Briefing";

    public string KeyHints => _mailFocused
        ? "↑↓ move · Tab to-dos · ⏎ open · a announcements · r refresh"
        : "↑↓ move · Tab mail · ⏎ open · d mark done · a announcements · r refresh";


    public HomeScreen(HubController hub)
    {
        _hub = hub;
        _todos = new SelectableList<PlannerItem>([], FormatTodo);
        _emails = new SelectableList<EmailSummary>([], FormatEmail);
    }


    // DATA ===================================================================================

    public async Task RefreshAsync(App app)
    {
        // One call, Canvas + Gmail fetched at the same time. If one side fails, the other still shows.
        var (ok, briefing) = await app.LoadAsync("Fetching Canvas & Gmail…", () => _hub.GetMorningBriefingAsync());
        _loaded = true;
        if (!ok) return;    // only happens on an unexpected bug; LoadAsync already showed the error

        _todoError = briefing.TodosError is null ? null : $"Couldn't load Canvas: {App.FriendlyError(briefing.TodosError)}\nPress r to retry.";
        _mailError = briefing.EmailsError is null ? null : $"Couldn't load Gmail: {App.FriendlyError(briefing.EmailsError)}\nPress r to retry.";

        if (briefing.Todos is not null) _todos.SetItems(briefing.Todos.OrderBy(t => t.Date ?? DateTime.MaxValue));   // soonest first, no-date last
        if (briefing.Emails is not null) _emails.SetItems(briefing.Emails);

        if (_todoError is not null && _mailError is not null) app.SetStatus("Couldn't load Canvas or Gmail.", StatusKind.Error);
        else if (_todoError is not null || _mailError is not null) app.SetStatus("Some data couldn't be loaded.", StatusKind.Warning);
    }


    // DRAWING ================================================================================

    public IRenderable Render(int width, int height)
    {
        if (width >= SideBySideMinWidth)
        {
            int leftWidth = width / 2;
            return new Layout("body").SplitColumns(
                new Layout("todos").Size(leftWidth).Update(TodoPanel(leftWidth, height)),
                new Layout("mail").Update(MailPanel(width - leftWidth, height)));
        }

        int topHeight = height / 2;
        return new Layout("body").SplitRows(
            new Layout("todos").Size(topHeight).Update(TodoPanel(width, topHeight)),
            new Layout("mail").Update(MailPanel(width, height - topHeight)));
    }

    private Panel TodoPanel(int width, int height)
    {
        string counter = _todos.Count > 0 ? $" ({_todos.Selected + 1}/{_todos.Count})" : "";
        string empty = _todoError ?? (_loaded ? "Nothing due. Nice!" : "Loading…");
        return MakePanel($"Canvas · To-do{counter}", _todos.Render(width - 4, height - 2, !_mailFocused, empty),
                         Theme.CanvasAccent, focused: !_mailFocused);
    }

    private Panel MailPanel(int width, int height)
    {
        string counter = _emails.Count > 0 ? $" ({_emails.Count})" : "";
        string empty = _mailError ?? (_loaded ? "Inbox zero. No unread mail." : "Loading…");
        return MakePanel($"Gmail · Unread{counter}", _emails.Render(width - 4, height - 2, _mailFocused, empty),
                         Theme.MailAccent, focused: _mailFocused);
    }

    // The focused panel gets a bright, heavy border; the other one is dimmed
    private static Panel MakePanel(string title, IRenderable content, Color accent, bool focused) => new(content)
    {
        Header = new PanelHeader($" {title} "),
        Border = focused ? BoxBorder.Heavy : BoxBorder.Rounded,
        BorderStyle = new Style(focused ? accent : Theme.Muted),
        Expand = true
    };

    private static ListLine FormatTodo(PlannerItem item)
    {
        var (due, dueColor) = Theme.DueLabel(item.Date?.ToLocalTime(), DateTime.Now);
        return new ListLine(
            Main: TextUtil.OneLine(item.Details?.Title ?? "(untitled)"),
            Tag: Theme.PlannerTag(item.Type),
            TagColor: Theme.CanvasAccent,
            Sub: [new(due, new Style(dueColor)), new($" · {TextUtil.OneLine(item.CourseName)}", new Style(Theme.Muted))]);
    }

    private static ListLine FormatEmail(EmailSummary email)
    {
        string subject = TextUtil.OneLine(email.Subject);
        return new ListLine(
            Main: subject.Length > 0 ? subject : "(no subject)",
            Sub: [new(Theme.SenderName(email.From), new Style(Color.Grey70)),
                  new($" · {Theme.EmailTime(email.Date, DateTime.Now)}", new Style(Theme.Muted))]);
    }


    // KEYS ===================================================================================

    public async Task<bool> HandleKeyAsync(ConsoleKeyInfo key, App app)
    {
        if (key.Key == ConsoleKey.Tab || Keys.Left(key) || Keys.Right(key))
        {
            _mailFocused = key.Key == ConsoleKey.Tab ? !_mailFocused : Keys.Right(key);
            return true;
        }

        if (_mailFocused ? _emails.HandleKey(key) : _todos.HandleKey(key)) return true;

        if (Keys.Enter(key))
        {
            if (_mailFocused) await OpenEmailAsync(app);
            else await OpenTodoAsync(app);
            return true;
        }

        switch (key.KeyChar)
        {
            case 'r':
                await RefreshAsync(app);
                if (_todoError is null && _mailError is null) app.SetStatus("Refreshed", StatusKind.Success);
                return true;
            case 'd' when !_mailFocused:
                await MarkTodoDoneAsync(app);
                return true;
            case 'a':
                await OpenCoursesAsync(app);
                return true;
        }
        return false;
    }


    // ACTIONS ================================================================================

    private async Task OpenEmailAsync(App app)
    {
        if (_emails.Current is not EmailSummary summary) return;

        // OpenEmailAsync also marks it as read -> it no longer belongs in the "unread" list
        var (ok, email) = await app.LoadAsync("Opening email…", () => _hub.OpenEmailAsync(summary.ID));
        if (!ok) return;
        _emails.Remove(summary);

        app.Push(new DetailScreen(
            kind: "Email",
            heading: email.Subject.Length > 0 ? email.Subject : "(no subject)",
            meta: [("From", email.From), ("To", email.To), ("Date", Theme.LongDate(email.Date))],
            body: email.Body,
            accent: Theme.MailAccent,
            actions:
            [
                new DetailAction('s', "star", async a =>
                {
                    var (starOk, starred) = await a.LoadAsync("Starring…", () => _hub.StarEmailAsync(email.ID));
                    if (starOk && starred) a.SetStatus("Starred", StatusKind.Success);
                    else if (starOk) a.SetStatus("Gmail didn't accept the star.", StatusKind.Error);
                }),
                new DetailAction('t', "trash", async a =>
                {
                    if (!await a.ConfirmAsync("Move this email to Trash?")) return;

                    var (trashOk, trashed) = await a.LoadAsync("Moving to Trash…", () => _hub.TrashEmailAsync(email.ID));
                    if (trashOk && trashed)
                    {
                        a.Pop();
                        a.SetStatus("Moved to Trash", StatusKind.Success);
                    }
                    else if (trashOk) a.SetStatus("Gmail didn't accept the trash request.", StatusKind.Error);
                })
            ]));
    }

    private async Task OpenTodoAsync(App app)
    {
        if (_todos.Current is not PlannerItem item) return;

        string courseID = item.CourseID.ToString();
        string title = item.Details?.Title ?? "(untitled)";

        switch (item.Type)
        {
            case "assignment":
            {
                var (ok, assignment) = await app.LoadAsync("Opening assignment…", () => _hub.GetAssignmentAsync(courseID, item.PlannableID));
                if (!ok) return;
                if (assignment is null) { app.SetStatus("Assignment not found.", StatusKind.Warning); return; }

                app.Push(new DetailScreen(
                    kind: assignment.IsQuizAssignment ? "Quiz" : "Assignment",
                    heading: assignment.Name,
                    meta: [("Course", item.CourseName), ("Due", Theme.LongDate(assignment.Deadline?.ToLocalTime())),
                           ("Points", assignment.TotalScore?.ToString() ?? "—")],
                    body: TextCleaner.CleanHtml(assignment.Description) is { Length: > 0 } description
                        ? description
                        : "(No description, or it's locked.)",
                    accent: Theme.CanvasAccent));
                break;
            }

            case "announcement" or "discussion_topic":
            {
                var (ok, topic) = await app.LoadAsync("Opening…", () => _hub.GetCourseAnnouncementAsync(courseID, item.PlannableID));
                if (!ok) return;
                if (topic is null) { app.SetStatus("Not found.", StatusKind.Warning); return; }

                app.Push(new DetailScreen(
                    kind: item.Type == "announcement" ? "Announcement" : "Discussion",
                    heading: topic.Title,
                    meta: [("Course", item.CourseName), ("By", topic.AuthorName ?? "Unknown"), ("Posted", Theme.LongDate(topic.PostedAt?.ToLocalTime()))],
                    body: TextCleaner.CleanHtml(topic.Message),
                    accent: Theme.CanvasAccent));
                break;
            }

            default:
                // Quizzes, pages, events, notes: no detail endpoint wired up yet, so show what the planner already knows
                app.Push(new DetailScreen(
                    kind: Theme.PlannerTag(item.Type),
                    heading: title,
                    meta: [("Course", item.CourseName), ("Due", Theme.LongDate(item.Date?.ToLocalTime())),
                           ("Points", item.Details?.TotalPoints?.ToString() ?? "—")],
                    body: "Open this in Canvas for the full details.",
                    accent: Theme.CanvasAccent));
                break;
        }
    }

    private async Task MarkTodoDoneAsync(App app)
    {
        if (_todos.Current is not PlannerItem item) return;

        string title = TextUtil.Truncate(TextUtil.OneLine(item.Details?.Title ?? "this item"), 40);
        if (!await app.ConfirmAsync($"Mark \"{title}\" as done?")) return;

        var (ok, result) = await app.LoadAsync("Marking as done…", () => _hub.CompleteTodoAsync(item.PlannableID));
        if (!ok) return;

        switch (result)
        {
            case CompleteTodoResult.Completed:
                _todos.Remove(item);
                app.SetStatus($"Marked \"{title}\" as done", StatusKind.Success);
                break;
            case CompleteTodoResult.NotFound:
                app.SetStatus("That item isn't in the feed anymore. Press r to refresh.", StatusKind.Warning);
                break;
            case CompleteTodoResult.Failed:
                app.SetStatus("Canvas rejected the update.", StatusKind.Error);
                break;
        }
    }

    // Courses list -> a course's announcements -> one announcement
    private async Task OpenCoursesAsync(App app)
    {
        var (ok, courses) = await app.LoadAsync("Fetching courses…", () => _hub.GetCurrentCoursesAsync());
        if (!ok) return;

        app.Push(new ListScreen<Course>(
            title: "Courses",
            items: courses,
            format: course => new ListLine(TextUtil.OneLine(course.Name), Sub: [new(course.CourseCode, new Style(Theme.Muted))]),
            open: OpenCourseAnnouncementsAsync,
            accent: Theme.CanvasAccent,
            emptyText: "No courses found for this semester."));
    }

    private async Task OpenCourseAnnouncementsAsync(Course course, App app)
    {
        var (ok, announcements) = await app.LoadAsync($"Fetching {course.CourseCode} announcements…",
                                                      () => _hub.GetCourseAnnouncementsAsync(course.ID.ToString()));
        if (!ok) return;

        app.Push(new ListScreen<CourseAnnouncement>(
            title: $"{course.CourseCode} · Announcements",
            items: announcements.OrderByDescending(a => a.PostedAt),
            format: a => new ListLine(TextUtil.OneLine(a.Title),
                Sub: [new(a.ProfessorName, new Style(Color.Grey70)),
                      new($" · {Theme.EmailTime(a.PostedAt?.ToLocalTime(), DateTime.Now)}", new Style(Theme.Muted))]),
            open: (a, appRef) =>
            {
                // The list request already includes each announcement's message, so no extra fetch needed
                appRef.Push(new DetailScreen(
                    kind: "Announcement",
                    heading: a.Title,
                    meta: [("Course", course.Name), ("By", a.ProfessorName), ("Posted", Theme.LongDate(a.PostedAt?.ToLocalTime()))],
                    body: TextCleaner.CleanHtml(a.Message),
                    accent: Theme.CanvasAccent));
                return Task.CompletedTask;
            },
            accent: Theme.CanvasAccent,
            emptyText: "No announcements in this course."));
    }
}
