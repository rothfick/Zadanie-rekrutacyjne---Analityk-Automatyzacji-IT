using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Ports;

public interface IComplaintRepository
{
    Task<Complaint?> GetByIdAsync(
        ComplaintId complaintId,
        CancellationToken cancellationToken = default);

    Task<Complaint?> GetByMessageIdAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Complaint>> ListAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default);
}
