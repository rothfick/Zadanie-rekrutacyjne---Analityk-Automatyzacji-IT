using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Domain.Events;

public sealed record EmailReceived(
    ComplaintId ComplaintId,
    string MessageId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record AttachmentsStored(
    ComplaintId ComplaintId,
    IReadOnlyCollection<AttachmentInfo> Attachments,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ComplaintParsed(
    ComplaintId ComplaintId,
    AiTriageResult Result,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DefectClassified(
    ComplaintId ComplaintId,
    DefectCategory DefectCategory,
    decimal ConfidenceScore,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CustomerMatched(
    ComplaintId ComplaintId,
    string CustomerId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record OrderVerified(
    ComplaintId ComplaintId,
    SapVerificationResult Result,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record BatchVerified(
    ComplaintId ComplaintId,
    SapVerificationResult Result,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record JiraComplaintCreated(
    ComplaintId ComplaintId,
    JiraIssueRef Issue,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ResponseDrafted(
    ComplaintId ComplaintId,
    string Draft,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record HumanReviewRequested(
    ComplaintId ComplaintId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record HumanReviewCompleted(
    ComplaintId ComplaintId,
    string Reviewer,
    string Decision,
    string? Notes,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CustomerClarificationRequested(
    ComplaintId ComplaintId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CorrectionTicketCreated(
    ComplaintId ComplaintId,
    JiraIssueRef Issue,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ComplaintClosed(
    ComplaintId ComplaintId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ComplaintFailed(
    ComplaintId ComplaintId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DuplicateLinked(
    ComplaintId ComplaintId,
    ComplaintId ExistingComplaintId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SapMismatchDetected(
    ComplaintId ComplaintId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
