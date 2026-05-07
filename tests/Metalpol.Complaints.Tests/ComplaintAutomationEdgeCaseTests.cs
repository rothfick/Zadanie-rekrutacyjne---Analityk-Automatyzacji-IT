using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Orchestration;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;
using Metalpol.Complaints.Infrastructure.Fakes;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ComplaintAutomationEdgeCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 8, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UnknownCustomerStopsBeforeSapAndJiraAndRequiresHumanReview()
    {
        var fixture = CreateFixture(
            TriageResult(),
            customerLookup: new UnmatchedCustomerLookupService());

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("edge-unknown-customer"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("Customer could not be matched", complaint.HumanReviewReason);
        Assert.Equal(0, fixture.Sap.OrderLookupCount);
        Assert.Equal(0, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task SapTimeoutRoutesToHumanReviewAndDoesNotCreateJiraComplaint()
    {
        var fixture = CreateFixture(
            TriageResult(),
            sap: new RecordingSapClient(orderException: new TimeoutException("SAP did not respond.")));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("edge-sap-timeout"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("SAP verification failed", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(0, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(SapMismatchDetected));
    }

    [Fact]
    public async Task SapRateLimitRoutesToHumanReviewWithoutCreatingJiraComplaint()
    {
        var fixture = CreateFixture(
            TriageResult(),
            sap: new RecordingSapClient(orderException: new InvalidOperationException("Simulated SAP rate limit.")));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("edge-sap-rate-limit"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("SAP verification failed", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(0, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task MissingSapBatchRoutesToHumanReviewAfterOrderVerification()
    {
        var fixture = CreateFixture(
            TriageResult(),
            sap: new RecordingSapClient(batchExists: false));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("edge-missing-sap-batch"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("SAP batch not found", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(1, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(OrderVerified));
        Assert.Contains(timeline, item => item.EventName == nameof(SapMismatchDetected));
    }

    [Fact]
    public async Task SapBatchTimeoutRoutesToHumanReviewWithoutCreatingJiraComplaint()
    {
        var fixture = CreateFixture(
            TriageResult(),
            sap: new RecordingSapClient(batchException: new TimeoutException("SAP batch endpoint did not respond.")));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("edge-sap-batch-timeout"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("SAP batch verification failed", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(1, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(SapMismatchDetected));
    }

    [Fact]
    public async Task MissingBatchDataFromAiAndSapRoutesToHumanReviewWithoutBatchLookup()
    {
        var fixture = CreateFixture(
            TriageResult(batchNumber: null),
            sap: new RecordingSapClient(orderBatchId: null));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("edge-missing-all-batch-data"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("SAP batch data missing", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(0, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(OrderVerified));
        Assert.DoesNotContain(timeline, item => item.EventName == nameof(BatchVerified));
    }

    [Fact]
    public async Task UnknownDefectCategoryRequiresHumanReviewButKeepsTraceabilityInJira()
    {
        var fixture = CreateFixture(TriageResult(
            category: DefectCategory.Unknown,
            confidence: 0.90m));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("edge-unknown-category"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Equal(DefectCategory.Unknown, complaint.DefectCategory);
        Assert.Equal("COMPLAINT-1001", complaint.JiraComplaint?.IssueKey);
        Assert.Contains("Defect category unknown", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task BlobUploadFailureStopsPipelineBeforeAiSapAndJira()
    {
        var blobStorage = new ThrowingBlobStorageClient();
        var fixture = CreateFixture(TriageResult(), blobStorage: blobStorage);
        var email = CreateEmail("edge-blob-failure");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Orchestrator.StartIntakeAsync(email));
        var complaint = await fixture.Repository.GetByMessageIdAsync(email.MessageId);

        Assert.Equal("Simulated blob upload failure.", exception.Message);
        Assert.NotNull(complaint);
        Assert.Equal(ComplaintStatus.IntakeQueued, complaint.Status);
        Assert.Equal(1, blobStorage.StoreCallCount);
        Assert.Equal(0, fixture.Ai.ExtractCallCount);
        Assert.Equal(0, fixture.Sap.OrderLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task JiraFailureBubblesAfterSapVerificationWithoutStoringJiraIssue()
    {
        var jira = new RecordingJiraClient(throwOnCreateComplaint: true);
        var fixture = CreateFixture(TriageResult(), jira: jira);
        var email = CreateEmail("edge-jira-failure");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Orchestrator.StartIntakeAsync(email));
        var complaint = await fixture.Repository.GetByMessageIdAsync(email.MessageId);

        Assert.Equal("Simulated Jira create failure.", exception.Message);
        Assert.NotNull(complaint);
        Assert.Equal(ComplaintStatus.SapVerified, complaint.Status);
        Assert.Null(complaint.JiraComplaint);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(1, fixture.Sap.BatchLookupCount);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
    }

    private static EdgeFixture CreateFixture(
        AiTriageResult triageResult,
        ICustomerLookupService? customerLookup = null,
        IBlobStorageClient? blobStorage = null,
        RecordingSapClient? sap = null,
        RecordingJiraClient? jira = null)
    {
        var repository = new InMemoryComplaintRepository();
        var eventLog = new InMemoryEventLog();
        var ai = new StaticAiTriageService(triageResult);
        var sapClient = sap ?? new RecordingSapClient();
        var jiraClient = jira ?? new RecordingJiraClient();
        var orchestrator = new ComplaintIntakeOrchestrator(
            repository,
            eventLog,
            blobStorage ?? new FakeBlobStorageClient(),
            ai,
            customerLookup ?? new FakeCustomerLookupService(),
            sapClient,
            jiraClient,
            new FixedClock(Now));

        return new EdgeFixture(orchestrator, repository, eventLog, ai, sapClient, jiraClient);
    }

    private static IncomingEmailDto CreateEmail(string messageId)
    {
        return new IncomingEmailDto(
            messageId,
            "quality@automotive-pl.example",
            "Complaint",
            "Complaint body.",
            Now,
            new[]
            {
                new IncomingAttachmentDto("att-001", "photo.jpg", "image/jpeg", 1024)
            });
    }

    private static AiTriageResult TriageResult(
        DefectCategory category = DefectCategory.Visual,
        decimal confidence = 0.92m,
        string? batchNumber = "BATCH-1001")
    {
        return new AiTriageResult(
            "en",
            "ORDER-1001",
            "Complaint description.",
            category,
            confidence,
            Array.Empty<string>(),
            "Complaint summary.",
            "Draft response.",
            batchNumber);
    }

    private sealed record EdgeFixture(
        ComplaintIntakeOrchestrator Orchestrator,
        InMemoryComplaintRepository Repository,
        InMemoryEventLog EventLog,
        StaticAiTriageService Ai,
        RecordingSapClient Sap,
        RecordingJiraClient Jira);

    private sealed class StaticAiTriageService : IAiTriageService
    {
        private readonly AiTriageResult _result;

        public StaticAiTriageService(AiTriageResult result)
        {
            _result = result;
        }

        public int ExtractCallCount { get; private set; }

        public Task<AiTriageResult> ExtractAsync(
            IncomingEmailDto email,
            CancellationToken cancellationToken = default)
        {
            ExtractCallCount++;

            return Task.FromResult(_result);
        }

        public Task<ResponseDraftDto> DraftResponseAsync(
            Complaint complaint,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ResponseDraftDto(
                language: "en",
                body: "Draft response.",
                requiresHumanReview: true,
                reviewReasons: new[] { "Review required." }));
        }
    }

    private sealed class UnmatchedCustomerLookupService : ICustomerLookupService
    {
        public Task<CustomerMatchDto> MatchByEmailAsync(
            string emailAddress,
            string? customerIdHint = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CustomerMatchDto(
                isMatched: false,
                customerId: null,
                displayName: null,
                emailDomain: null,
                confidenceScore: 0m));
        }
    }

    private sealed class ThrowingBlobStorageClient : IBlobStorageClient
    {
        public int StoreCallCount { get; private set; }

        public Task<IReadOnlyCollection<AttachmentInfo>> StoreAttachmentsAsync(
            ComplaintId complaintId,
            IReadOnlyCollection<IncomingAttachmentDto> attachments,
            CancellationToken cancellationToken = default)
        {
            StoreCallCount++;

            throw new InvalidOperationException("Simulated blob upload failure.");
        }
    }

    private sealed class RecordingSapClient : ISapClient
    {
        private readonly bool _batchExists;
        private readonly Exception? _orderException;
        private readonly Exception? _batchException;
        private readonly string? _orderBatchId;

        public RecordingSapClient(
            bool batchExists = true,
            Exception? orderException = null,
            Exception? batchException = null,
            string? orderBatchId = "BATCH-1001")
        {
            _batchExists = batchExists;
            _orderException = orderException;
            _batchException = batchException;
            _orderBatchId = orderBatchId;
        }

        public int OrderLookupCount { get; private set; }

        public int BatchLookupCount { get; private set; }

        public Task<SapOrderDto> GetOrderAsync(
            string orderId,
            CancellationToken cancellationToken = default)
        {
            OrderLookupCount++;

            if (_orderException is not null)
            {
                throw _orderException;
            }

            return Task.FromResult(new SapOrderDto(
                orderId,
                exists: true,
                customerId: "CUST-AUTOMOTIVE-PL",
                batchId: _orderBatchId,
                productionLine: "LINE-1",
                status: "Delivered"));
        }

        public Task<SapBatchDto> GetBatchAsync(
            string batchId,
            CancellationToken cancellationToken = default)
        {
            BatchLookupCount++;

            if (_batchException is not null)
            {
                throw _batchException;
            }

            return Task.FromResult(new SapBatchDto(
                batchId,
                exists: _batchExists,
                orderId: "ORDER-1001",
                productionLine: _batchExists ? "LINE-1" : null,
                productionDate: _batchExists ? new DateOnly(2026, 5, 8) : null));
        }
    }

    private sealed class RecordingJiraClient : IJiraClient
    {
        private readonly bool _throwOnCreateComplaint;
        private int _nextComplaintNumber = 1001;

        public RecordingJiraClient(bool throwOnCreateComplaint = false)
        {
            _throwOnCreateComplaint = throwOnCreateComplaint;
        }

        public int CreatedComplaintCount { get; private set; }

        public Task<JiraIssueDto> CreateComplaintAsync(
            Complaint complaint,
            CancellationToken cancellationToken = default)
        {
            CreatedComplaintCount++;

            if (_throwOnCreateComplaint)
            {
                throw new InvalidOperationException("Simulated Jira create failure.");
            }

            var key = $"COMPLAINT-{_nextComplaintNumber++}";

            return Task.FromResult(new JiraIssueDto(key, "Complaint", "Open", $"mock://jira/{key}"));
        }

        public Task<JiraIssueDto> CreateCorrectionAsync(
            Complaint complaint,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new JiraIssueDto("CORRECTION-2001", "Correction", "Open", "mock://jira/CORRECTION-2001"));
        }

        public Task<JiraIssueDto> UpdateIssueAsync(
            string issueKey,
            string status,
            IReadOnlyDictionary<string, string>? fields = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new JiraIssueDto(issueKey, "Complaint", status, $"mock://jira/{issueKey}"));
        }
    }
}
