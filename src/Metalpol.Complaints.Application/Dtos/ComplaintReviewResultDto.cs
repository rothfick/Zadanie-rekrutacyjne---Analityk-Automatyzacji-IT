using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Dtos;

public sealed record ComplaintReviewResultDto(
    bool Succeeded,
    ComplaintId? ComplaintId,
    ComplaintStatus? Status,
    string? CorrectionIssueKey = null,
    string? Error = null)
{
    public static ComplaintReviewResultDto Success(
        ComplaintId complaintId,
        ComplaintStatus status,
        string? correctionIssueKey = null)
    {
        return new ComplaintReviewResultDto(true, complaintId, status, correctionIssueKey);
    }

    public static ComplaintReviewResultDto Failure(
        ComplaintId complaintId,
        string error)
    {
        return new ComplaintReviewResultDto(false, complaintId, null, Error: error);
    }
}
