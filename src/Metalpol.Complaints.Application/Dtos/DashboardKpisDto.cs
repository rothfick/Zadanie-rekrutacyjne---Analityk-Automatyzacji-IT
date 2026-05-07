using Metalpol.Complaints.Domain.Enums;

namespace Metalpol.Complaints.Application.Dtos;

public sealed record DashboardKpisDto
{
    public DashboardKpisDto(
        TimeSpan averageTimeToIngestEmail,
        TimeSpan averageFirstResponseTime,
        TimeSpan averageResolutionTime,
        int backlogSize,
        int slaBreachCount,
        IReadOnlyDictionary<DefectCategory, int>? complaintCountByDefectCategory,
        IReadOnlyDictionary<string, int>? complaintCountByProductionLine,
        IReadOnlyDictionary<string, int>? complaintCountByBatch,
        decimal manualReviewRatePercent,
        IReadOnlyDictionary<string, int>? aiExtractionConfidenceDistribution,
        decimal classificationCorrectionRatePercent,
        decimal jiraIssueCreationSuccessRatePercent,
        decimal sapVerificationFailureRatePercent)
    {
        DtoValidation.RequireNonNegative(averageTimeToIngestEmail, nameof(averageTimeToIngestEmail), "Ingest time cannot be negative.");
        DtoValidation.RequireNonNegative(averageFirstResponseTime, nameof(averageFirstResponseTime), "First response time cannot be negative.");
        DtoValidation.RequireNonNegative(averageResolutionTime, nameof(averageResolutionTime), "Resolution time cannot be negative.");
        DtoValidation.RequireNonNegative(backlogSize, nameof(backlogSize), "Backlog size cannot be negative.");
        DtoValidation.RequireNonNegative(slaBreachCount, nameof(slaBreachCount), "SLA breach count cannot be negative.");
        DtoValidation.RequirePercent(manualReviewRatePercent, nameof(manualReviewRatePercent));
        DtoValidation.RequirePercent(classificationCorrectionRatePercent, nameof(classificationCorrectionRatePercent));
        DtoValidation.RequirePercent(jiraIssueCreationSuccessRatePercent, nameof(jiraIssueCreationSuccessRatePercent));
        DtoValidation.RequirePercent(sapVerificationFailureRatePercent, nameof(sapVerificationFailureRatePercent));

        AverageTimeToIngestEmail = averageTimeToIngestEmail;
        AverageFirstResponseTime = averageFirstResponseTime;
        AverageResolutionTime = averageResolutionTime;
        BacklogSize = backlogSize;
        SlaBreachCount = slaBreachCount;
        ComplaintCountByDefectCategory = DtoValidation.CopyCountMap(complaintCountByDefectCategory, nameof(complaintCountByDefectCategory));
        ComplaintCountByProductionLine = DtoValidation.CopyCountMap(complaintCountByProductionLine, nameof(complaintCountByProductionLine));
        ComplaintCountByBatch = DtoValidation.CopyCountMap(complaintCountByBatch, nameof(complaintCountByBatch));
        ManualReviewRatePercent = manualReviewRatePercent;
        AiExtractionConfidenceDistribution = DtoValidation.CopyCountMap(aiExtractionConfidenceDistribution, nameof(aiExtractionConfidenceDistribution));
        ClassificationCorrectionRatePercent = classificationCorrectionRatePercent;
        JiraIssueCreationSuccessRatePercent = jiraIssueCreationSuccessRatePercent;
        SapVerificationFailureRatePercent = sapVerificationFailureRatePercent;
    }

    public TimeSpan AverageTimeToIngestEmail { get; }

    public TimeSpan AverageFirstResponseTime { get; }

    public TimeSpan AverageResolutionTime { get; }

    public int BacklogSize { get; }

    public int SlaBreachCount { get; }

    public IReadOnlyDictionary<DefectCategory, int> ComplaintCountByDefectCategory { get; }

    public IReadOnlyDictionary<string, int> ComplaintCountByProductionLine { get; }

    public IReadOnlyDictionary<string, int> ComplaintCountByBatch { get; }

    public decimal ManualReviewRatePercent { get; }

    public IReadOnlyDictionary<string, int> AiExtractionConfidenceDistribution { get; }

    public decimal ClassificationCorrectionRatePercent { get; }

    public decimal JiraIssueCreationSuccessRatePercent { get; }

    public decimal SapVerificationFailureRatePercent { get; }
}
