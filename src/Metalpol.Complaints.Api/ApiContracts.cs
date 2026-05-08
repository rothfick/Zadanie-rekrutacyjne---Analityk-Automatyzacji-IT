using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;

namespace Metalpol.Complaints.Api;

public sealed record MockExchangeMessageRequest(
    string SourceMessageId,
    string From,
    string? Subject,
    string Body,
    IReadOnlyCollection<IncomingAttachmentDto>? Attachments = null)
{
    public IncomingEmailDto ToIncomingEmail(IClock clock)
    {
        return new IncomingEmailDto(
            SourceMessageId,
            From,
            Subject ?? string.Empty,
            Body,
            clock.UtcNow,
            Attachments ?? Array.Empty<IncomingAttachmentDto>());
    }
}

public sealed record ComplaintIntakeResponse(
    string ComplaintId,
    ComplaintStatus Status,
    string MessageId,
    string? OrderNumber,
    string? BatchNumber,
    DefectCategory DefectCategory,
    decimal? AiConfidence,
    string? JiraComplaintKey,
    bool HumanReviewRequired,
    bool Duplicate);

public sealed record ComplaintDetailsResponse(
    string ComplaintId,
    string MessageId,
    ComplaintStatus Status,
    DateTimeOffset ReceivedAt,
    DefectCategory DefectCategory,
    string? CustomerId,
    string? OrderNumber,
    string? BatchNumber,
    decimal? AiConfidence,
    bool PromptInjectionDetected,
    IReadOnlyCollection<string> MissingFields,
    string? JiraComplaintKey,
    string? CorrectionTicketKey,
    string? ResponseDraft,
    string? HumanReviewReason,
    IReadOnlyCollection<AttachmentResponse> Attachments);

public sealed record AttachmentResponse(
    string AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? StorageUri);

public sealed record ReviewApprovalRequest(
    string Reviewer,
    ComplaintReviewDecision Decision,
    string? Notes);

public sealed record ReviewApprovalResponse(
    bool Succeeded,
    string ComplaintId,
    ComplaintStatus? Status,
    string? CorrectionIssueKey,
    string? Error);

public sealed record DashboardKpiResponse(
    int TotalComplaints,
    int BacklogSize,
    int HumanReviewRequired,
    int CorrectionsCreated,
    int ClosedComplaints,
    int SlaBreachCount,
    IReadOnlyDictionary<DefectCategory, int> ComplaintCountByDefectCategory,
    IReadOnlyDictionary<string, int> ComplaintCountByProductionLine,
    IReadOnlyDictionary<string, int> ComplaintCountByBatch,
    decimal PercentRequiringHumanReview,
    IReadOnlyDictionary<string, int> AiExtractionConfidenceDistribution,
    decimal JiraIssueCreationSuccessRatePercent,
    decimal SapVerificationFailureRatePercent);

public static class ApiContractMapper
{
    public static ComplaintIntakeResponse ToIntakeResponse(Complaint complaint, bool duplicate = false)
    {
        return new ComplaintIntakeResponse(
            complaint.Id.Value,
            complaint.Status,
            complaint.MessageId,
            complaint.AiTriage?.OrderNumber,
            complaint.AiTriage?.BatchNumber ?? complaint.BatchVerification?.BatchId,
            complaint.DefectCategory,
            complaint.AiTriage?.ConfidenceScore,
            complaint.JiraComplaint?.IssueKey,
            complaint.Status == ComplaintStatus.HumanReviewRequired,
            duplicate);
    }

    public static ComplaintDetailsResponse ToDetailsResponse(Complaint complaint)
    {
        return new ComplaintDetailsResponse(
            complaint.Id.Value,
            complaint.MessageId,
            complaint.Status,
            complaint.ReceivedAt,
            complaint.DefectCategory,
            complaint.CustomerId,
            complaint.AiTriage?.OrderNumber,
            complaint.AiTriage?.BatchNumber ?? complaint.BatchVerification?.BatchId,
            complaint.AiTriage?.ConfidenceScore,
            complaint.AiTriage?.PromptInjectionDetected ?? false,
            complaint.AiTriage?.MissingFields ?? Array.Empty<string>(),
            complaint.JiraComplaint?.IssueKey,
            complaint.CorrectionTicket?.IssueKey,
            complaint.ResponseDraft,
            complaint.HumanReviewReason,
            complaint.Attachments.Select(attachment => new AttachmentResponse(
                attachment.AttachmentId,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.StorageUri)).ToArray());
    }

    public static ReviewApprovalResponse ToReviewResponse(ComplaintReviewResultDto result)
    {
        return new ReviewApprovalResponse(
            result.Succeeded,
            result.ComplaintId?.Value ?? string.Empty,
            result.Status,
            result.CorrectionIssueKey,
            result.Error);
    }

    public static DashboardKpiResponse ToDashboardResponse(
        IReadOnlyCollection<Complaint> complaints,
        int sapVerificationFailureCount = 0)
    {
        var total = complaints.Count;
        var denominator = total == 0 ? 1 : total;
        var humanReviewCount = complaints.Count(complaint => complaint.Status == ComplaintStatus.HumanReviewRequired);
        var jiraCreatedCount = complaints.Count(complaint => complaint.JiraComplaint is not null);

        return new DashboardKpiResponse(
            TotalComplaints: total,
            BacklogSize: complaints.Count(IsBacklogItem),
            HumanReviewRequired: humanReviewCount,
            CorrectionsCreated: complaints.Count(complaint => complaint.CorrectionTicket is not null),
            ClosedComplaints: complaints.Count(complaint => complaint.Status == ComplaintStatus.Closed),
            SlaBreachCount: 0,
            ComplaintCountByDefectCategory: complaints
                .GroupBy(complaint => complaint.DefectCategory)
                .ToDictionary(group => group.Key, group => group.Count()),
            ComplaintCountByProductionLine: complaints
                .Select(complaint => complaint.BatchVerification?.ProductionLine ?? complaint.OrderVerification?.ProductionLine)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value!)
                .ToDictionary(group => group.Key, group => group.Count()),
            ComplaintCountByBatch: complaints
                .Select(complaint => complaint.BatchVerification?.BatchId ?? complaint.AiTriage?.BatchNumber)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value!)
                .ToDictionary(group => group.Key, group => group.Count()),
            PercentRequiringHumanReview: Math.Round(humanReviewCount * 100m / denominator, 2),
            AiExtractionConfidenceDistribution: complaints
                .Select(complaint => complaint.AiTriage?.ConfidenceScore)
                .Where(confidence => confidence.HasValue)
                .GroupBy(confidence => ConfidenceBucket(confidence!.Value))
                .ToDictionary(group => group.Key, group => group.Count()),
            JiraIssueCreationSuccessRatePercent: Math.Round(jiraCreatedCount * 100m / denominator, 2),
            SapVerificationFailureRatePercent: Math.Round(sapVerificationFailureCount * 100m / denominator, 2));
    }

    private static bool IsBacklogItem(Complaint complaint)
    {
        return complaint.Status is not ComplaintStatus.Closed
            and not ComplaintStatus.Failed
            and not ComplaintStatus.CorrectionCreated
            and not ComplaintStatus.DuplicateLinked;
    }

    private static string ConfidenceBucket(decimal confidence)
    {
        return confidence switch
        {
            >= 0.85m => "0.85-1.00",
            >= 0.60m => "0.60-0.84",
            _ => "0.00-0.59"
        };
    }
}
