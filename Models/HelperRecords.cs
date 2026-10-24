namespace CanvasMail.Models;


public enum CompleteTodoResult { Completed, NotFound, Failed }

public record MorningBriefing(
    IReadOnlyList<PlannerItem>? Todos, Exception? TodosError,
    List<EmailSummary>? Emails, Exception? EmailsError
);