namespace Metalpol.Complaints.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
