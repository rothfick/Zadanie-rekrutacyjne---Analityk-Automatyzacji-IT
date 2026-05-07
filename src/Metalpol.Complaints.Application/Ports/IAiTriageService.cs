using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Ports;

public interface IAiTriageService
{
    Task<AiTriageResult> ExtractAsync(
        IncomingEmailDto email,
        CancellationToken cancellationToken = default);

    Task<ResponseDraftDto> DraftResponseAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default);
}
