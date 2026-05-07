using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Ports;

public interface IEventLog
{
    Task AppendAsync(
        ComplaintId complaintId,
        IReadOnlyCollection<IDomainEvent> events,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ComplaintTimelineItemDto>> GetTimelineAsync(
        ComplaintId complaintId,
        CancellationToken cancellationToken = default);
}
