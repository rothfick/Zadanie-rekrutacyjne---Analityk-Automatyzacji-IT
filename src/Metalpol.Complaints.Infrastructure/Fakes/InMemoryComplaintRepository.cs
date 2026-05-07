using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class InMemoryComplaintRepository : IComplaintRepository
{
    private readonly Dictionary<string, Complaint> _complaintsById = new();

    public Task<Complaint?> GetByIdAsync(
        ComplaintId complaintId,
        CancellationToken cancellationToken = default)
    {
        _complaintsById.TryGetValue(complaintId.Value, out var complaint);

        return Task.FromResult(complaint);
    }

    public Task<Complaint?> GetByMessageIdAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        var complaint = _complaintsById.Values.FirstOrDefault(item => item.MessageId == messageId);

        return Task.FromResult(complaint);
    }

    public Task<IReadOnlyCollection<Complaint>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<Complaint>>(_complaintsById.Values.ToArray());
    }

    public Task SaveAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(complaint);

        _complaintsById[complaint.Id.Value] = complaint;

        return Task.CompletedTask;
    }

    public void Clear()
    {
        _complaintsById.Clear();
    }
}
