namespace CanvasMail.Models;

// Create and use our own records because Google's own `Message` is very big and somewhat weird or unintuitive, at least on this use case.

public record EmailSummary(
    string ID,
    string From,
    string Subject,
    DateTime? Date,
    string Snippet
);

public record EmailDetail(
    string ID,
    string From,
    string To,
    string Subject,
    DateTime? Date,
    string Body
);