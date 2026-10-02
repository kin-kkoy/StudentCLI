using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanvasRecords;


public record Course(
    [property: JsonPropertyName("id")] long ID,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("course_code")] string CourseCode,
    [property: JsonPropertyName("created_at")] DateTime? CourseCreationDate
);

public record CourseAnnouncement(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("posted_at")] DateTime? PostedAt,
    [property: JsonPropertyName("user_name")] string ProfessorName,
    [property: JsonPropertyName("attachments")] List<JsonElement>? Attachments,   // Just for checking if there are attachments
    [property: JsonPropertyName("message")] string Message
);

// This is for being able to mark an item in the planner feed as "completed/read"
public record PlannerOverride(
    [property: JsonPropertyName("id")] long ID,
    [property: JsonPropertyName("marked_complete")] bool MarkedComplete,
    [property: JsonPropertyName("dismissed")] bool Dismissed
);

public record PlannerItemDetails(
    [property: JsonPropertyName("id")] long ID,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("points_possible")] double? TotalPoints
);

public record PlannerItem(
    [property: JsonPropertyName("plannable_id")] long PlannableID, // to send a completion request to canvas
    [property: JsonPropertyName("course_id")] long CourseID,
    [property: JsonPropertyName("context_name")] string CourseName,
    [property: JsonPropertyName("plannable_type")] string Type,
    [property: JsonPropertyName("plannable_date")] DateTime? Date,
    [property: JsonPropertyName("planner_override")] PlannerOverride? Override,
    [property: JsonPropertyName("plannable")] PlannerItemDetails? Details
);