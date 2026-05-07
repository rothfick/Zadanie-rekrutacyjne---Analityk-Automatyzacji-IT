using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Review;

public interface IComplaintReviewService
{
    Task<ComplaintReviewResultDto> ApproveComplaintAsync(
        ComplaintId complaintId,
        string reviewer,
        ComplaintReviewDecision decision,
        string? notes = null,
        CancellationToken cancellationToken = default);
}
