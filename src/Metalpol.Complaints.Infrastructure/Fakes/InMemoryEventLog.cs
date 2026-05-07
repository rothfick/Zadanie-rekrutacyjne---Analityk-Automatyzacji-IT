using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class InMemoryEventLog : IEventLog
{
    private readonly Dictionary<string, List<ComplaintTimelineItemDto>> _timelineByComplaintId = new();

    public Task AppendAsync(
        ComplaintId complaintId,
        IReadOnlyCollection<IDomainEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (!_timelineByComplaintId.TryGetValue(complaintId.Value, out var timeline))
        {
            timeline = new List<ComplaintTimelineItemDto>();
            _timelineByComplaintId[complaintId.Value] = timeline;
        }

        timeline.AddRange(events.Select(domainEvent => new ComplaintTimelineItemDto(
            domainEvent.GetType().Name,
            domainEvent.OccurredAt,
            "Domain")));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ComplaintTimelineItemDto>> GetTimelineAsync(
        ComplaintId complaintId,
        CancellationToken cancellationToken = default)
    {
        var timeline = _timelineByComplaintId.TryGetValue(complaintId.Value, out var items)
            ? items.OrderBy(item => item.OccurredAt).ToArray()
            : Array.Empty<ComplaintTimelineItemDto>();

        return Task.FromResult<IReadOnlyCollection<ComplaintTimelineItemDto>>(timeline);
    }

    public void Clear()
    {
        _timelineByComplaintId.Clear();
    }
}
