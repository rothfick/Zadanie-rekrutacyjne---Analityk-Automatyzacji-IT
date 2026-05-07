namespace Metalpol.Complaints.Application.Dtos;

public sealed record ComplaintTimelineItemDto
{
    public ComplaintTimelineItemDto(
        string eventName,
        DateTimeOffset occurredAt,
        string source,
        string? description = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        DtoValidation.RequireNotBlank(eventName, nameof(eventName), "Timeline event name is required.");
        DtoValidation.RequireNotBlank(source, nameof(source), "Timeline source is required.");

        EventName = eventName;
        OccurredAt = occurredAt;
        Source = source;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        Metadata = DtoValidation.CopyStringMap(metadata);
    }

    public string EventName { get; }

    public DateTimeOffset OccurredAt { get; }

    public string Source { get; }

    public string? Description { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }
}
