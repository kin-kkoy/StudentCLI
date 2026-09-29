using Google.Apis.Auth.OAuth2;

using CanvasRecords;
using TerminalHub;


DotNetEnv.Env.Load();


var googleClientSecrets = new ClientSecrets
{
    ClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not found in .env"),
    ClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not found in .env")
};


// ================================== CANVAS ==================================

string domain = Environment.GetEnvironmentVariable("CANVAS_DOMAIN") ?? throw new InvalidOperationException("CANVAS_DOMAIN not found in .env");
string accessToken = Environment.GetEnvironmentVariable("CANVAS_TOKEN") ?? throw new InvalidOperationException("CANVAS_TOKEN not found in .env");

var canvas = new CanvasService(domain, accessToken);
Console.WriteLine("Connecting to Canvas...\n");

// List<CourseAnnouncement> announcements = await canvas.FetchCourseAnnouncementsAsync(testID);

// foreach(var announcement in announcements)
// {
//     Console.WriteLine($"[ID: {announcement.Id}]");
//     Console.WriteLine($"By: {announcement.ProfessorName}  on  {announcement.PostedAt: MM-dd-yyyy}");
//     Console.WriteLine(new string('-', 50)); // replace this later with message if ever.
// }


// Getting Todos
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