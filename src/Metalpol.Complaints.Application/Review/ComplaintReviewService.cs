using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Review;

public sealed class ComplaintReviewService : IComplaintReviewService
{
    private readonly IComplaintRepository _complaints;
    private readonly IEventLog _eventLog;
    private readonly IJiraClient _jira;
    private readonly IClock _clock;

    public ComplaintReviewService(
        IComplaintRepository complaints,
        IEventLog eventLog,
        IJiraClient jira,
        IClock clock)
    {
        _complaints = complaints;
        _eventLog = eventLog;
        _jira = jira;
        _clock = clock;
    }

    public async Task<ComplaintReviewResultDto> ApproveComplaintAsync(
        ComplaintId complaintId,
        string reviewer,
        ComplaintReviewDecision decision,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var complaint = await _complaints.GetByIdAsync(complaintId, cancellationToken);
        if (complaint is null)
        {
            return ComplaintReviewResultDto.Failure(
                complaintId,
                $"Complaint {complaintId.Value} was not found.");
        }

        try
        {
            return decision switch
            {
                ComplaintReviewDecision.ConfirmDefect => await ConfirmDefectAsync(
                    complaint,
                    reviewer,
                    notes,
                    cancellationToken),
                ComplaintReviewDecision.RequestMoreInfo => await RequestMoreInfoAsync(
                    complaint,
                    reviewer,
                    notes,
                    cancellationToken),
                ComplaintReviewDecision.RejectComplaint => await RejectComplaintAsync(
                    complaint,
                    reviewer,
                    notes,
                    cancellationToken),
                _ => ComplaintReviewResultDto.Failure(complaintId, $"Unsupported review decision: {decision}.")
            };
        }
        catch (DomainException exception)
        {
            return ComplaintReviewResultDto.Failure(complaintId, exception.Message);
        }
    }

    private async Task<ComplaintReviewResultDto> ConfirmDefectAsync(
        Complaint complaint,
        string reviewer,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (complaint.CorrectionTicket is not null)
        {
            return ComplaintReviewResultDto.Success(
                complaint.Id,
                complaint.Status,
                complaint.CorrectionTicket.IssueKey);
        }

        complaint.RecordHumanReview(
            reviewer,
            ComplaintReviewDecision.ConfirmDefect.ToString(),
            notes,
            _clock.UtcNow);

        if (complaint.Status != ComplaintStatus.CustomerResponseApproved)
        {
            complaint.ApproveCustomerResponse();
        }

        var correction = await _jira.CreateCorrectionAsync(complaint, cancellationToken);
        complaint.CreateCorrectionTicket(
            new JiraIssueRef(correction.IssueKey, correction.IssueType, correction.Url),
            _clock.UtcNow);

        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        return ComplaintReviewResultDto.Success(
            complaint.Id,
            complaint.Status,
            correction.IssueKey);
    }

    private async Task<ComplaintReviewResultDto> RequestMoreInfoAsync(
        Complaint complaint,
        string reviewer,
        string? notes,
        CancellationToken cancellationToken)
    {
        complaint.RequestCustomerClarification(
            reviewer,
            ComplaintReviewDecision.RequestMoreInfo.ToString(),
            notes,
            _clock.UtcNow);

        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        return ComplaintReviewResultDto.Success(complaint.Id, complaint.Status);
    }

    private async Task<ComplaintReviewResultDto> RejectComplaintAsync(
        Complaint complaint,
        string reviewer,
        string? notes,
        CancellationToken cancellationToken)
    {
        complaint.RejectAfterReview(
            reviewer,
            ComplaintReviewDecision.RejectComplaint.ToString(),
            notes,
            _clock.UtcNow);

        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        return ComplaintReviewResultDto.Success(complaint.Id, complaint.Status);
    }

    private async Task SaveAndAppendEventsAsync(
        Complaint complaint,
        CancellationToken cancellationToken)
    {
        var events = complaint.DomainEvents.ToArray();

        await _complaints.SaveAsync(complaint, cancellationToken);

        if (events.Length > 0)
        {
            await _eventLog.AppendAsync(complaint.Id, events, cancellationToken);
            complaint.ClearDomainEvents();
        }
    }
}
