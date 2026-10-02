using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using CanvasRecords;

namespace TerminalHub;


/*  ARCHITECTURE:
    ┌────────────────────────────────────────────────────────┐
    │ 1. Data Contracts (Record DTOs)                        │
    │    - Define what data we accept from the JSON payload  │
    ├────────────────────────────────────────────────────────┤
    │ 2. State & Constructor (HttpClient Setup)              │
    │    - BaseAddress, User-Agent, Bearer, Accept headers   │
    ├────────────────────────────────────────────────────────┤
    │ 3. Action Methods (Async HTTP Pipeline)                │  <-- Talking to Canvas
    │    - GET -> Validate -> Read Stream -> Deserialize     │
    └────────────────────────────────────────────────────────┘
*/


public class CanvasService
{
    // Field
    //  this here helps us make HTTP requests (get, post, etc.) over the network through an API
    private readonly HttpClient _http;



    // Constructor
    public CanvasService(string hostDomain, string apiSecretKey)
    {
        // 2 things: initialize the http client -> then attach it with the necessary headers.

        //  1.) Initialize the client caller w/ the base address
        _http = new HttpClient
        {
            // Attaching base address
            BaseAddress = new Uri($"https://{hostDomain}/api/v1/")  // apparently the trailing/last `/` is needed to allow relative or upcoming route joins (other routs)
        };

        // 2.) Attach necessary Authorization Headers:

        //  Canvas strictly requires a User-Agent
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TerminalHub/1.0 (StudentCLI)");
        //  -> "Bearer: <token/authorization>"
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiSecretKey);
        //  -> Accept header: "application/json"
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    }


    // Talking to Canvas ===========================================================================

    public async Task<List<CourseAnnouncement>> FetchCourseAnnouncementsAsync(string courseId)
    {
        // 1. Send the GET request to the relative path
        // 2. Throw an exception if status is not 200-299
        // 3. Read the payload body as a raw string
        // 4. Deserialize the JSON string into our typed List<CourseAnnouncement>
        // 5.  ====  Afterwards return or do what you want with the object.
        /*  Diagram Version:
                1. Send HTTP request
                        ↓
                2. Check response
                        ↓
                3. Read response body
                        ↓
                4. Deserialize JSON
                        ↓
                5. Return C# object
            or
                Canvas API
                    ↓
                HttpClient sends GET
                    ↓
                HTTP response
                    ↓
                JSON text
                    ↓
                deserialize
                    ↓
                List<CourseAnnouncement>
        */


        //  1.   (This is relative to the BaseAddress, we're simply appending)
        HttpResponseMessage response = await _http.GetAsync($"announcements?context_codes[]=course_{courseId}");

        //  2.
        response.EnsureSuccessStatusCode();

        //  3.
        string jsonString = await response.Content.ReadAsStringAsync();

        //  4.  Return list of announcements, or an empty one if none.
        List<CourseAnnouncement> announcements = JsonSerializer.Deserialize<List<CourseAnnouncement>>(jsonString) ?? new List<CourseAnnouncement>();


        return announcements;
    }

    // Fetch a single announcement from a course. Default: Latest announcement (read or not)
    public async Task<AnnouncementDetail?> FetchAnnouncementAsync(string courseID, long announcementID)
    {
        HttpResponseMessage response = await _http.GetAsync($"courses/{courseID}/discussion_topics/{announcementID}");
        response.EnsureSuccessStatusCode();

        string responsePayload = await response.Content.ReadAsStringAsync();

        AnnouncementDetail? announcement = JsonSerializer.Deserialize<AnnouncementDetail>(responsePayload);
        
        return announcement;
    }

    // Fetch courses in that school year
    public async Task<List<Course>> FetchCurrentSemesterCoursesAsync(DateTime semesterDate)
    {
        var response = await _http.GetAsync("users/self/favorites/courses");
        response.EnsureSuccessStatusCode();

        string responsePayload = await response.Content.ReadAsStringAsync();

        List<Course> courses = JsonSerializer.Deserialize<List<Course>>(responsePayload) ?? new List<Course>();

        // trim out return the courses in the current sem
        return courses.Where(course => course.CourseCreationDate >= semesterDate).ToList();
    }

    public async Task<List<PlannerItem>> FetchDashboardFeedAsync(int daysAhead = 14, bool showCompleted = false)
    {
        // Always start at the first day of the current week
        DateTime today = DateTime.UtcNow.Date;
        int daysSinceSunday = ((int)today.DayOfWeek - (int)DayOfWeek.Sunday + 7) % 7;

        // Canvas accepts ISO-8601 strings: YYYY-MM-DD
        string startDate = DateTime.UtcNow.AddDays(-daysSinceSunday).ToString("yyyy-MM-dd");
        string endDate = DateTime.UtcNow.AddDays(daysAhead).ToString("yyyy-MM-dd");

        string url = $"planner/items?start_date={startDate}&end_date={endDate}";
        if(!showCompleted)  url += "&filter=incomplete_items";

        HttpResponseMessage response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string jsonPayload = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<PlannerItem>>(jsonPayload) ?? new List<PlannerItem>();
    }

    // Mark an item on the dashboard feed as completed
    public async Task<bool> MarkItemCompleteAsync(PlannerItem item)
    {
        // An override will only be made when the user (me) interacts with the item (except reading or viewing)

        // Case 1: If an override already exists, PUT to update it
        if(item.Override != null)
        {
            var updatePayload = new { marked_complete = true };
            var putResponse = await _http.PutAsJsonAsync($"planner/overrides/{item.Override.ID}", updatePayload);
            return putResponse.IsSuccessStatusCode;
        }

        // Case 2: No override exists yet, POST to create one
        var createPayload = new // required parameters by Canvas
        {
            plannable_type = item.Type,
            plannable_id = item.PlannableID,
            marked_complete = true
        };

        var postResponse = await _http.PostAsJsonAsync("planner/overrides", createPayload);
        return postResponse.IsSuccessStatusCode;
    }

}