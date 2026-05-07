using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Orchestration;

public interface IComplaintOrchestrator
{
    Task<ComplaintIntakeResultDto> StartIntakeAsync(
        IncomingEmailDto email,
        CancellationToken cancellationToken = default);
}
