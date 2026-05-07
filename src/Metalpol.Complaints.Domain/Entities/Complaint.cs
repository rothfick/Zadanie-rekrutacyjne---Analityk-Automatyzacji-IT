using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Domain.Entities;

public sealed class Complaint
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<AttachmentInfo> _attachments = new();

    private bool _orderVerified;

    private Complaint(ComplaintId id, string messageId, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Message id cannot be empty.", nameof(messageId));
        }

        Id = id;
        MessageId = messageId;
        Status = ComplaintStatus.Received;
        Priority = ComplaintPriority.Normal;
        ReceivedAt = receivedAt;

        AddEvent(new EmailReceived(Id, MessageId, receivedAt));
    }

    public ComplaintId Id { get; }

    public string MessageId { get; }

    public ComplaintStatus Status { get; private set; }

    public ComplaintPriority Priority { get; private set; }

    public DateTimeOffset ReceivedAt { get; }

    public AiTriageResult? AiTriage { get; private set; }

    public DefectCategory DefectCategory { get; private set; } = DefectCategory.Unknown;

    public string? CustomerId { get; private set; }

    public SapVerificationResult? OrderVerification { get; private set; }

    public SapVerificationResult? BatchVerification { get; private set; }

    public JiraIssueRef? JiraComplaint { get; private set; }

    public JiraIssueRef? CorrectionTicket { get; private set; }

    public string? ResponseDraft { get; private set; }

    public string? HumanReviewReason { get; private set; }

    public string? FailureReason { get; private set; }

    public ComplaintId? LinkedDuplicateComplaintId { get; private set; }

    public IReadOnlyCollection<AttachmentInfo> Attachments => _attachments.AsReadOnly();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static Complaint ReceiveEmail(
        ComplaintId id,
        string messageId,
        DateTimeOffset? receivedAt = null)
    {
        return new Complaint(id, messageId, receivedAt ?? DateTimeOffset.UtcNow);
    }

    public void QueueIntake()
    {
        EnsureStatus(ComplaintStatus.Received, "Only received complaints can be queued for intake.");

        Status = ComplaintStatus.IntakeQueued;
    }

    public void StoreAttachments(
        IReadOnlyCollection<AttachmentInfo> attachments,
        DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.IntakeQueued, ComplaintStatus.Parsed, ComplaintStatus.MissingData },
            "Attachments can only be stored during intake or clarification.");

        if (attachments.Count == 0)
        {
            throw new ArgumentException("At least one attachment must be provided.", nameof(attachments));
        }

        _attachments.Clear();
        _attachments.AddRange(attachments);

        AddEvent(new AttachmentsStored(Id, Attachments, At(occurredAt)));
    }

    public void Parse(AiTriageResult result, DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.IntakeQueued, ComplaintStatus.MissingData },
            "Complaint can only be parsed after intake or customer clarification.");

        AiTriage = result;
        DefectCategory = result.DefectCategory;
        Status = result.HasMissingFields
            ? ComplaintStatus.MissingData
            : ComplaintStatus.Parsed;

        var timestamp = At(occurredAt);
        AddEvent(new ComplaintParsed(Id, result, timestamp));
        AddEvent(new DefectClassified(Id, result.DefectCategory, result.ConfidenceScore, timestamp));
    }

    public void MatchCustomer(string customerId, DateTimeOffset? occurredAt = null)
    {
        EnsureStatus(ComplaintStatus.Parsed, "Customer can only be matched after successful parsing.");

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("Customer id cannot be empty.", nameof(customerId));
        }

        CustomerId = customerId;
        Status = ComplaintStatus.CustomerMatched;

        AddEvent(new CustomerMatched(Id, customerId, At(occurredAt)));
    }

    public void MarkSapVerificationPending()
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.CustomerMatched, ComplaintStatus.SapVerificationPending },
            "SAP verification can only be pending after customer match.");

        Status = ComplaintStatus.SapVerificationPending;
    }

    public void VerifyOrder(SapVerificationResult result, DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.CustomerMatched, ComplaintStatus.SapVerificationPending },
            "Order can only be verified after customer match.");

        if (!result.IsVerified)
        {
            MarkSapMismatch(result.FailureReason ?? "Order verification failed.", occurredAt);
            return;
        }

        OrderVerification = result;
        _orderVerified = true;

        AddEvent(new OrderVerified(Id, result, At(occurredAt)));
    }

    public void VerifyBatch(SapVerificationResult result, DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.CustomerMatched, ComplaintStatus.SapVerificationPending },
            "Batch can only be verified after customer match.");

        if (!_orderVerified)
        {
            throw new DomainException("Order must be verified before batch verification.");
        }

        if (!result.IsVerified)
        {
            MarkSapMismatch(result.FailureReason ?? "Batch verification failed.", occurredAt);
            return;
        }

        BatchVerification = result;
        Status = ComplaintStatus.SapVerified;

        AddEvent(new BatchVerified(Id, result, At(occurredAt)));
    }

    public void MarkSapMismatch(string reason, DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.CustomerMatched, ComplaintStatus.SapVerificationPending },
            "SAP mismatch can only be recorded after customer match.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("SAP mismatch reason cannot be empty.", nameof(reason));
        }

        Status = ComplaintStatus.SapMismatch;

        AddEvent(new SapMismatchDetected(Id, reason, At(occurredAt)));
    }

    public void CreateJiraComplaint(JiraIssueRef issue, DateTimeOffset? occurredAt = null)
    {
        EnsureStatus(ComplaintStatus.SapVerified, "Jira Complaint can only be created after SAP verification.");

        JiraComplaint = issue;
        Status = ComplaintStatus.JiraComplaintCreated;

        AddEvent(new JiraComplaintCreated(Id, issue, At(occurredAt)));
    }

    public void DraftResponse(string draft, DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.JiraComplaintCreated, ComplaintStatus.HumanReviewRequired },
            "Response can only be drafted after Jira Complaint creation or during human review.");

        if (string.IsNullOrWhiteSpace(draft))
        {
            throw new ArgumentException("Response draft cannot be empty.", nameof(draft));
        }

        ResponseDraft = draft;
        if (Status != ComplaintStatus.HumanReviewRequired)
        {
            Status = ComplaintStatus.ResponseDrafted;
        }

        AddEvent(new ResponseDrafted(Id, draft, At(occurredAt)));
    }

    public void RequestHumanReview(string reason, DateTimeOffset? occurredAt = null)
    {
        EnsureNotTerminal("Human review cannot be requested for a terminal complaint.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Human review reason cannot be empty.", nameof(reason));
        }

        HumanReviewReason = reason;
        Status = ComplaintStatus.HumanReviewRequired;

        AddEvent(new HumanReviewRequested(Id, reason, At(occurredAt)));
    }

    public void ApproveCustomerResponse()
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.ResponseDrafted, ComplaintStatus.HumanReviewRequired },
            "Customer response can only be approved after draft or human review.");

        Status = ComplaintStatus.CustomerResponseApproved;
    }

    public void RecordHumanReview(
        string reviewer,
        string decision,
        string? notes = null,
        DateTimeOffset? occurredAt = null)
    {
        EnsureNotTerminal("Human review cannot be completed for a terminal complaint.");
        ValidateReview(reviewer, decision);

        AddEvent(new HumanReviewCompleted(Id, reviewer, decision, Normalize(notes), At(occurredAt)));
    }

    public void RequestCustomerClarification(
        string reviewer,
        string decision,
        string? notes = null,
        DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.HumanReviewRequired, ComplaintStatus.ResponseDrafted, ComplaintStatus.MissingData },
            "Customer clarification can only be requested during review or after a draft.");
        ValidateReview(reviewer, decision);

        var reason = string.IsNullOrWhiteSpace(notes)
            ? "Customer clarification requested."
            : notes;

        HumanReviewReason = reason;
        Status = ComplaintStatus.MissingData;

        var timestamp = At(occurredAt);
        AddEvent(new HumanReviewCompleted(Id, reviewer, decision, Normalize(notes), timestamp));
        AddEvent(new CustomerClarificationRequested(Id, reason, timestamp));
    }

    public void RejectAfterReview(
        string reviewer,
        string decision,
        string? notes = null,
        DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[] { ComplaintStatus.HumanReviewRequired, ComplaintStatus.ResponseDrafted, ComplaintStatus.MissingData },
            "Complaint can only be rejected during review or after a draft.");
        ValidateReview(reviewer, decision);

        Status = ComplaintStatus.Closed;

        var timestamp = At(occurredAt);
        AddEvent(new HumanReviewCompleted(Id, reviewer, decision, Normalize(notes), timestamp));
        AddEvent(new ComplaintClosed(Id, timestamp));
    }

    public void CreateCorrectionTicket(JiraIssueRef issue, DateTimeOffset? occurredAt = null)
    {
        EnsureStatus(ComplaintStatus.CustomerResponseApproved, "Correction ticket can only be created after customer response approval.");

        CorrectionTicket = issue;
        Status = ComplaintStatus.CorrectionCreated;

        AddEvent(new CorrectionTicketCreated(Id, issue, At(occurredAt)));
    }

    public void LinkDuplicate(ComplaintId existingComplaintId, DateTimeOffset? occurredAt = null)
    {
        EnsureNotTerminal("Duplicate cannot be linked for a terminal complaint.");

        if (existingComplaintId == Id)
        {
            throw new DomainException("Complaint cannot be linked as a duplicate of itself.");
        }

        LinkedDuplicateComplaintId = existingComplaintId;
        Status = ComplaintStatus.DuplicateLinked;

        AddEvent(new DuplicateLinked(Id, existingComplaintId, At(occurredAt)));
    }

    public void Close(DateTimeOffset? occurredAt = null)
    {
        EnsureAnyStatus(
            new[]
            {
                ComplaintStatus.CustomerResponseApproved,
                ComplaintStatus.CorrectionCreated,
                ComplaintStatus.DuplicateLinked
            },
            "Complaint can only be closed after approval, correction creation or duplicate link.");

        Status = ComplaintStatus.Closed;

        AddEvent(new ComplaintClosed(Id, At(occurredAt)));
    }

    public void Fail(string reason, DateTimeOffset? occurredAt = null)
    {
        EnsureNotTerminal("Terminal complaint cannot fail again.");

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason cannot be empty.", nameof(reason));
        }

        FailureReason = reason;
        Status = ComplaintStatus.Failed;

        AddEvent(new ComplaintFailed(Id, reason, At(occurredAt)));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void EnsureStatus(ComplaintStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new DomainException(message);
        }
    }

    private void EnsureAnyStatus(IReadOnlyCollection<ComplaintStatus> expected, string message)
    {
        if (!expected.Contains(Status))
        {
            throw new DomainException(message);
        }
    }

    private void EnsureNotTerminal(string message)
    {
        if (Status is ComplaintStatus.Closed or ComplaintStatus.Failed)
        {
            throw new DomainException(message);
        }
    }

    private static void ValidateReview(string reviewer, string decision)
    {
        if (string.IsNullOrWhiteSpace(reviewer))
        {
            throw new ArgumentException("Reviewer cannot be empty.", nameof(reviewer));
        }

        if (string.IsNullOrWhiteSpace(decision))
        {
            throw new ArgumentException("Review decision cannot be empty.", nameof(decision));
        }
    }

    private void AddEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    private static DateTimeOffset At(DateTimeOffset? occurredAt) => occurredAt ?? DateTimeOffset.UtcNow;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
