using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Orchestration;

public sealed class ComplaintIntakeOrchestrator : IComplaintOrchestrator
{
    private const decimal HumanReviewConfidenceThreshold = 0.85m;

    private readonly IComplaintRepository _complaints;
    private readonly IEventLog _eventLog;
    private readonly IBlobStorageClient _blobStorage;
    private readonly IAiTriageService _aiTriage;
    private readonly ICustomerLookupService _customerLookup;
    private readonly ISapClient _sap;
    private readonly IJiraClient _jira;
    private readonly IClock _clock;

    public ComplaintIntakeOrchestrator(
        IComplaintRepository complaints,
        IEventLog eventLog,
        IBlobStorageClient blobStorage,
        IAiTriageService aiTriage,
        ICustomerLookupService customerLookup,
        ISapClient sap,
        IJiraClient jira,
        IClock clock)
    {
        _complaints = complaints;
        _eventLog = eventLog;
        _blobStorage = blobStorage;
        _aiTriage = aiTriage;
        _customerLookup = customerLookup;
        _sap = sap;
        _jira = jira;
        _clock = clock;
    }

    public async Task<ComplaintIntakeResultDto> StartIntakeAsync(
        IncomingEmailDto email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var existingComplaint = await _complaints.GetByMessageIdAsync(email.MessageId, cancellationToken);
        if (existingComplaint is not null)
        {
            await _eventLog.AppendAsync(
                existingComplaint.Id,
                new[] { new DuplicateLinked(existingComplaint.Id, existingComplaint.Id, _clock.UtcNow) },
                cancellationToken);

            return ToResult(existingComplaint);
        }

        var complaint = Complaint.ReceiveEmail(CreateComplaintId(email.MessageId), email.MessageId, email.ReceivedAt);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        complaint.QueueIntake();
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        if (email.Attachments.Count > 0)
        {
            var storedAttachments = await _blobStorage.StoreAttachmentsAsync(
                complaint.Id,
                email.Attachments,
                cancellationToken);

            complaint.StoreAttachments(storedAttachments, _clock.UtcNow);
            await SaveAndAppendEventsAsync(complaint, cancellationToken);
        }

        var triage = await _aiTriage.ExtractAsync(email, cancellationToken);
        complaint.Parse(triage, _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        var reviewReasons = GetTriageReviewReasons(triage);
        if (triage.HasMissingFields)
        {
            return await RequestReviewAndDraftAsync(
                complaint,
                reviewReasons,
                GetDraftFromTriage(triage),
                cancellationToken);
        }

        var customer = await _customerLookup.MatchByEmailAsync(email.FromEmail, cancellationToken: cancellationToken);
        if (!customer.IsMatched || string.IsNullOrWhiteSpace(customer.CustomerId))
        {
            reviewReasons.Add("Customer could not be matched.");

            return await RequestReviewAndDraftAsync(
                complaint,
                reviewReasons,
                GetDraftFromTriage(triage),
                cancellationToken);
        }

        complaint.MatchCustomer(customer.CustomerId, _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        var sapVerified = await VerifyInSapAsync(complaint, triage, reviewReasons, cancellationToken);
        if (!sapVerified)
        {
            return await RequestReviewAndDraftAsync(
                complaint,
                reviewReasons,
                GetDraftFromTriage(triage),
                cancellationToken);
        }

        var jiraIssue = await _jira.CreateComplaintAsync(complaint, cancellationToken);
        complaint.CreateJiraComplaint(
            new JiraIssueRef(jiraIssue.IssueKey, jiraIssue.IssueType, jiraIssue.Url),
            _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        complaint.DraftResponse(GetDraftFromTriage(triage), _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        if (reviewReasons.Count > 0)
        {
            complaint.RequestHumanReview(FormatReviewReasons(reviewReasons), _clock.UtcNow);
            await SaveAndAppendEventsAsync(complaint, cancellationToken);
        }

        return ToResult(complaint);
    }

    private async Task<bool> VerifyInSapAsync(
        Complaint complaint,
        AiTriageResult triage,
        ICollection<string> reviewReasons,
        CancellationToken cancellationToken)
    {
        complaint.MarkSapVerificationPending();
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        SapOrderDto order;
        try
        {
            order = await _sap.GetOrderAsync(triage.OrderNumber!, cancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or InvalidOperationException)
        {
            complaint.MarkSapMismatch($"SAP order verification failed: {exception.Message}", _clock.UtcNow);
            await SaveAndAppendEventsAsync(complaint, cancellationToken);
            reviewReasons.Add("SAP verification failed.");

            return false;
        }

        if (!order.Exists)
        {
            complaint.VerifyOrder(SapVerificationResult.Failed("Order not found in SAP."), _clock.UtcNow);
            await SaveAndAppendEventsAsync(complaint, cancellationToken);
            reviewReasons.Add("SAP order not found.");

            return false;
        }

        complaint.VerifyOrder(
            SapVerificationResult.Verified(order.OrderId, order.BatchId, order.ProductionLine),
            _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        var batchId = FirstNotBlank(triage.BatchNumber, order.BatchId);
        if (string.IsNullOrWhiteSpace(batchId))
        {
            reviewReasons.Add("SAP batch data missing.");

            return false;
        }

        SapBatchDto batch;
        try
        {
            batch = await _sap.GetBatchAsync(batchId, cancellationToken);
        }
        catch (Exception exception) when (exception is TimeoutException or InvalidOperationException)
        {
            complaint.MarkSapMismatch($"SAP batch verification failed: {exception.Message}", _clock.UtcNow);
            await SaveAndAppendEventsAsync(complaint, cancellationToken);
            reviewReasons.Add("SAP batch verification failed.");

            return false;
        }

        if (!batch.Exists)
        {
            complaint.VerifyBatch(SapVerificationResult.Failed("Batch not found in SAP."), _clock.UtcNow);
            await SaveAndAppendEventsAsync(complaint, cancellationToken);
            reviewReasons.Add("SAP batch not found.");

            return false;
        }

        complaint.VerifyBatch(
            SapVerificationResult.Verified(order.OrderId, batch.BatchId, batch.ProductionLine),
            _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        return true;
    }

    private async Task<ComplaintIntakeResultDto> RequestReviewAndDraftAsync(
        Complaint complaint,
        IReadOnlyCollection<string> reviewReasons,
        string draft,
        CancellationToken cancellationToken)
    {
        complaint.RequestHumanReview(FormatReviewReasons(reviewReasons), _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        complaint.DraftResponse(draft, _clock.UtcNow);
        await SaveAndAppendEventsAsync(complaint, cancellationToken);

        return ToResult(complaint);
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

    private static List<string> GetTriageReviewReasons(AiTriageResult triage)
    {
        var reasons = new List<string>();

        if (triage.ConfidenceScore < HumanReviewConfidenceThreshold)
        {
            reasons.Add("AI confidence below threshold.");
        }

        if (triage.HasMissingFields)
        {
            reasons.Add($"Missing required fields: {string.Join(", ", triage.MissingFields)}.");
        }

        if (triage.PromptInjectionDetected)
        {
            reasons.Add("Prompt injection pattern detected.");
        }

        if (triage.DefectCategory == DefectCategory.Unknown)
        {
            reasons.Add("Defect category unknown.");
        }

        return reasons;
    }

    private static ComplaintId CreateComplaintId(string messageId)
    {
        var normalized = new string(messageId
            .Select(character => char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '-')
            .ToArray());

        normalized = string.Join("-", normalized.Split('-', StringSplitOptions.RemoveEmptyEntries));

        return new ComplaintId($"CMP-{normalized}");
    }

    private static string GetDraftFromTriage(AiTriageResult triage)
    {
        return string.IsNullOrWhiteSpace(triage.CustomerResponseDraft)
            ? "Thank you for your complaint. A service specialist will review the case before any final response is sent."
            : triage.CustomerResponseDraft;
    }

    private static string FormatReviewReasons(IReadOnlyCollection<string> reasons)
    {
        return reasons.Count == 0
            ? "Human review required."
            : string.Join(" ", reasons.Distinct());
    }

    private static string? FirstNotBlank(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static ComplaintIntakeResultDto ToResult(Complaint complaint)
    {
        return new ComplaintIntakeResultDto(complaint.Id, complaint.Status);
    }
}
