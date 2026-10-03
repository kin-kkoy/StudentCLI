using Google.Apis.Auth.OAuth2;
using Google.Apis.Util;

using CanvasMail.Records.CanvasRecords;
using CanvasMail.TerminalHub;
using CanvasMail.Utility;


DotNetEnv.Env.Load();


var googleClientSecrets = new ClientSecrets
{
    ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not found in .env"),
    ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not found in .env")
};


// ================================== CANVAS ==================================

// For testing purposes:
string domain = Environment.GetEnvironmentVariable("CANVAS_DOMAIN") ?? throw new InvalidOperationException("CANVAS_DOMAIN not found in .env");
string accessToken = Environment.GetEnvironmentVariable("CANVAS_TOKEN") ?? throw new InvalidOperationException("CANVAS_TOKEN not found in .env");

var canvas = new CanvasService(domain, accessToken);
Console.WriteLine("Connecting to Canvas...\n");

if(canvas is null)
{
    Console.WriteLine("Canvas not loaded properly");
    Environment.Exit(0);
}

// Menu - lazy load style
ShowMenu();
Console.WriteLine("\nFetching Courses...");
List<Course> courses = await canvas.FetchCurrentSemesterCoursesAsync(new DateTime(2026, 08, 01)); // Sem is hardcoded for now
Console.WriteLine("=== COURSES:");
foreach (var course in courses)
{
    Console.WriteLine($"[ {course.ID} ]\t{course.CourseCode} -  {course.Name}");
}
Console.WriteLine("------------------------------------");
int choice = GetChoice();

switch (choice)
{
    case 1:
        await FetchDashboard();
        break;
    case 2:
        await FetchAnnouncements();
        break;
    case 3:
        await FetchSingleAnnouncement();
        break;
    case 4:
        await FetchSingleAssignment();
        break;
    default:
        Console.WriteLine("Choice not found! Exiting program");
        Environment.Exit(0);
        break;
}


async Task FetchAnnouncements()
{
    Console.Write("Course Choice:  ");
    string courseID = Console.ReadLine() ?? "00abcdefg";

    List<CourseAnnouncement> announcements = await canvas.FetchCourseAnnouncementsAsync(courseID);

    foreach(var announcement in announcements)
    {
        Console.WriteLine($"[ID: {announcement.Id}]");
        Console.WriteLine($"By: {announcement.ProfessorName}  on  {announcement.PostedAt: MM-dd-yyyy}");
        Console.WriteLine(new string('-', 50)); // replace this later with message if ever.
    }    
}

async Task FetchSingleAnnouncement()
{
    Console.Write("Course Choice:  ");
    string courseID = Console.ReadLine() ?? "00abcdefg";
    
    Console.Write("Announcement Choice:  ");
    long.TryParse(Console.ReadLine(), out long announcementID);
    
    AnnouncementDetail? announcement = await canvas.FetchAnnouncementAsync(courseID, announcementID);

    if(announcement is not null)
    {
        Console.WriteLine("========================================");
        Console.WriteLine($"TITLE:   {announcement.Title}");
        Console.WriteLine($"AUTHOR:  {announcement.AuthorName ?? "Unknown"}");
        Console.WriteLine($"POSTED:  {announcement.PostedAt?.ToLocalTime():MMM dd, yyyy h:mm tt}");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("CONTENTS:");
        Console.WriteLine(TextCleaner.CleanHtml(announcement.Message) ?? "[No Content]");
        Console.WriteLine("========================================");
    }
    else
    {
        Console.WriteLine("Announcement not found or loaded.");
    }
}

async Task FetchSingleAssignment()
{
    Console.Write("Course Choice:  ");
    string courseID = Console.ReadLine() ?? "00abcdefg";
    
    Console.Write("Assignment Choice:  ");
    long.TryParse(Console.ReadLine(), out long assignmentID);

    Assignment? assignment = await canvas.FetchAssignmentAsync(courseID, assignmentID);

    if (assignment is not null)
    {
        Console.WriteLine("========================================");
        Console.WriteLine($"[ASSIGNMENT] {assignment.Name}");
        Console.WriteLine($"Points:   {assignment.TotalScore?.ToString() ?? "No pts"}");
        Console.WriteLine($"Due:      {assignment.Deadline?.ToLocalTime():MMM dd, yyyy h:mm tt}");
        Console.WriteLine($"Is Quiz:  {assignment.IsQuizAssignment}");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("INSTRUCTIONS / PROMPT (Raw HTML):");
        Console.WriteLine(TextCleaner.CleanHtml(assignment.Description) ?? "[No Description Found / Locked]");
        Console.WriteLine("========================================");
    }
    else
    {
        Console.WriteLine("Assignment not found or loaded.");
    }
}

// Getting Todos
async Task FetchDashboard()
{
    List<PlannerItem> feed = await canvas.FetchDashboardFeedAsync();

    Console.WriteLine($"Found {feed.Count} to-do item(s).\n");

    foreach(var item in feed)
    {
        string course = item.CourseName;
        string typeTag = item.Type.ToUpper();
        string title = item.Details?.Title ?? "<No Title>";
        string dateString = item.Date.HasValue ? item.Date.Value.ToLocalTime().ToString("MMM dd, yyyy h:mm tt") : "No date";
        string totalScore = item.Details?.TotalPoints.HasValue == true ? $"{item.Details.TotalPoints} pts total" : "No pts";

        Console.WriteLine("-------------------------------");
        Console.WriteLine($"Course:\t{course}");
        Console.WriteLine($"[{typeTag}]\t ID: {item.PlannableID}");
        Console.WriteLine($"Title:\t{title}");
        Console.WriteLine($"Due:\t{dateString}\t -- {totalScore} ");
        Console.WriteLine("-------------------------------");
    }

    Console.Write("Mark an item as completed? (true/false):  ");
    bool.TryParse(Console.ReadLine(), out bool markItemChoice);
    if (markItemChoice)
    {
        // Test: completing a task
        Console.Write("Item ID to be Marked as Completed:  ");
        long itemID = long.Parse(Console.ReadLine() ?? "0000"); // it's a test anyways so no tryparse
        foreach(var item in feed)
        {
            if(item.PlannableID == itemID)
            {
                Console.WriteLine("Item Found! Marking as Completed...");
                bool isSuccess = await canvas.MarkItemCompleteAsync(item);
                if (isSuccess)
                {
                    Console.WriteLine("Successfully marked as completed!");
                }
                else
                {
                    Console.WriteLine("Failed to update status on Canvas.");
                }
                return;
            }
        }
    }
}

int GetChoice()
{
    Console.Write("Menu Choice:  ");
    int choice = int.Parse(Console.ReadLine() ?? "0");
    return choice;
}

void ShowMenu()
{
    Console.WriteLine("================== MENU ===================");
    Console.WriteLine("[ 1 ]\tGet Dashboard contents");
    Console.WriteLine("[ 2 ]\tGet Course Announcements");
    Console.WriteLine("[ 3 ]\tGet Course Announcement (single)");
    Console.WriteLine("[ 4 ]\tGet Course Assignment (single)");
    Console.WriteLine("===========================================");
}