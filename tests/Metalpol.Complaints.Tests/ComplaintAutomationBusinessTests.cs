using Metalpol.Complaints.Api;
using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Orchestration;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Application.Review;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.ValueObjects;
using Metalpol.Complaints.Infrastructure.Fakes;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ComplaintAutomationBusinessTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HappyPathCreatesComplaintJiraIssueForVerifiedComplaint()
    {
        var fixture = CreateIntakeFixture(TriageResult(confidence: 0.92m));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("business-happy-path"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.ResponseDrafted, result.Status);
        Assert.NotNull(complaint);
        Assert.Equal("COMPLAINT-1001", complaint.JiraComplaint?.IssueKey);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(1, fixture.Sap.BatchLookupCount);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task HumanApprovalOfConfirmedDefectCreatesCorrectionJiraIssue()
    {
        var fixture = CreateIntakeFixture(TriageResult(confidence: 0.92m));
        var intake = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("business-approval"));
        var reviewService = new ComplaintReviewService(
            fixture.Repository,
            fixture.EventLog,
            fixture.Jira,
            fixture.Clock);

        var review = await reviewService.ApproveComplaintAsync(
            intake.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.ConfirmDefect,
            "Defect confirmed by service specialist.");

        Assert.True(review.Succeeded);
        Assert.Equal(ComplaintStatus.CorrectionCreated, review.Status);
        Assert.Equal("CORRECTION-2001", review.CorrectionIssueKey);
        Assert.Equal(1, fixture.Jira.CreatedCorrectionCount);
    }

    [Fact]
    public async Task MissingOrderNumberRequiresHumanReviewAndDoesNotCallSapOrderVerification()
    {
        var fixture = CreateIntakeFixture(new AiTriageResult(
            "en",
            orderNumber: null,
            description: "Customer sent photos but no order number.",
            DefectCategory.Unknown,
            0.50m,
            new[] { "orderNumber" },
            "Order number missing.",
            "Please provide the order number."));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("business-missing-order"));

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.Equal(0, fixture.Sap.OrderLookupCount);
        Assert.Equal(0, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task UnknownSapOrderGoesToHumanReviewRequiredWithoutComplaintJiraIssue()
    {
        var sap = new RecordingSapClient(orderExists: false);
        var fixture = CreateIntakeFixture(TriageResult(confidence: 0.92m), sap);

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("business-sap-not-found"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("SAP order not found", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(0, fixture.Sap.BatchLookupCount);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task PromptInjectionIsFlaggedAndRequiresHumanReview()
    {
        var fixture = CreateIntakeFixture(TriageResult(confidence: 0.45m, promptInjectionDetected: true));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("business-prompt-injection"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.True(complaint.AiTriage?.PromptInjectionDetected);
        Assert.Contains("Prompt injection pattern detected", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task DuplicateSourceMessageIdIsIdempotentAndDoesNotCreateSecondJiraIssue()
    {
        var fixture = CreateIntakeFixture(TriageResult(confidence: 0.92m));
        var email = CreateEmail("business-duplicate-message");

        var first = await fixture.Orchestrator.StartIntakeAsync(email);
        var second = await fixture.Orchestrator.StartIntakeAsync(email);

        Assert.Equal(first.ComplaintId, second.ComplaintId);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
        Assert.Equal(1, fixture.Sap.OrderLookupCount);
        Assert.Equal(1, fixture.Sap.BatchLookupCount);
    }

    [Fact]
    public async Task LowConfidenceCategoryRequiresHumanReviewEvenWhenSapVerificationSucceeds()
    {
        var fixture = CreateIntakeFixture(TriageResult(
            category: DefectCategory.Dimensional,
            confidence: 0.70m));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("business-low-confidence"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Equal(DefectCategory.Dimensional, complaint.DefectCategory);
        Assert.Contains("AI confidence below threshold", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task DashboardKpisCountProcessedComplaintsAndManualReviewCases()
    {
        var repository = new InMemoryComplaintRepository();
        var eventLog = new InMemoryEventLog();
        var sap = new RecordingSapClient();
        var jira = new RecordingJiraClient();
        var clock = new FixedClock(Now);

        var happyPath = CreateOrchestrator(repository, eventLog, sap, jira, clock, TriageResult(confidence: 0.92m));
        await happyPath.StartIntakeAsync(CreateEmail("business-kpi-happy"));

        var manualReview = CreateOrchestrator(
            repository,
            eventLog,
            sap,
            jira,
            clock,
            new AiTriageResult(
                "en",
                orderNumber: null,
                description: "Missing order number.",
                DefectCategory.Unknown,
                0.50m,
                new[] { "orderNumber" },
                "Order number missing.",
                "Please provide the order number."));
        await manualReview.StartIntakeAsync(CreateEmail("business-kpi-review"));

        var complaints = await repository.ListAsync();
        var kpis = ApiContractMapper.ToDashboardResponse(complaints);

        Assert.Equal(2, kpis.TotalComplaints);
        Assert.Equal(1, kpis.HumanReviewRequired);
        Assert.Equal(50m, kpis.PercentRequiringHumanReview);
        Assert.Equal(1, kpis.ComplaintCountByDefectCategory[DefectCategory.Visual]);
        Assert.Equal(1, kpis.ComplaintCountByDefectCategory[DefectCategory.Unknown]);
    }

    private static IntakeFixture CreateIntakeFixture(
        AiTriageResult triageResult,
        RecordingSapClient? sap = null)
    {
        var repository = new InMemoryComplaintRepository();
        var eventLog = new InMemoryEventLog();
        var sapClient = sap ?? new RecordingSapClient();
        var jira = new RecordingJiraClient();
        var clock = new FixedClock(Now);
        var orchestrator = CreateOrchestrator(repository, eventLog, sapClient, jira, clock, triageResult);

        return new IntakeFixture(orchestrator, repository, eventLog, sapClient, jira, clock);
    }

    private static ComplaintIntakeOrchestrator CreateOrchestrator(
        InMemoryComplaintRepository repository,
        InMemoryEventLog eventLog,
        RecordingSapClient sap,
        RecordingJiraClient jira,
        FixedClock clock,
        AiTriageResult triageResult)
    {
        return new ComplaintIntakeOrchestrator(
            repository,
            eventLog,
            new FakeBlobStorageClient(),
            new StaticAiTriageService(triageResult),
            new FakeCustomerLookupService(),
            sap,
            jira,
            clock);
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
        string orderNumber = "ORDER-1001",
        string? batchNumber = "BATCH-1001",
        DefectCategory category = DefectCategory.Visual,
        decimal confidence = 0.92m,
        bool promptInjectionDetected = false)
    {
        return new AiTriageResult(
            "en",
            orderNumber,
            "Complaint description.",
            category,
            confidence,
            Array.Empty<string>(),
            "Complaint summary.",
            "Draft response.",
            batchNumber,
            promptInjectionDetected);
    }

    private sealed record IntakeFixture(
        ComplaintIntakeOrchestrator Orchestrator,
        InMemoryComplaintRepository Repository,
        InMemoryEventLog EventLog,
        RecordingSapClient Sap,
        RecordingJiraClient Jira,
        FixedClock Clock);

    private sealed class StaticAiTriageService : IAiTriageService
    {
        private readonly AiTriageResult _result;

        public StaticAiTriageService(AiTriageResult result)
        {
            _result = result;
        }

        public Task<AiTriageResult> ExtractAsync(
            IncomingEmailDto email,
            CancellationToken cancellationToken = default)
        {
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

    private sealed class RecordingSapClient : ISapClient
    {
        private readonly bool _orderExists;

        public RecordingSapClient(bool orderExists = true)
        {
            _orderExists = orderExists;
        }

        public int OrderLookupCount { get; private set; }

        public int BatchLookupCount { get; private set; }

        public Task<SapOrderDto> GetOrderAsync(
            string orderId,
            CancellationToken cancellationToken = default)
        {
            OrderLookupCount++;

            return Task.FromResult(_orderExists
                ? new SapOrderDto(orderId, exists: true, "CUST-AUTOMOTIVE-PL", "BATCH-1001", "LINE-1", "Delivered")
                : new SapOrderDto(orderId, exists: false));
        }

        public Task<SapBatchDto> GetBatchAsync(
            string batchId,
            CancellationToken cancellationToken = default)
        {
            BatchLookupCount++;

            return Task.FromResult(new SapBatchDto(batchId, exists: true, "ORDER-1001", "LINE-1", new DateOnly(2026, 5, 8)));
        }
    }

    private sealed class RecordingJiraClient : IJiraClient
    {
        private int _nextComplaintNumber = 1001;
        private int _nextCorrectionNumber = 2001;

        public int CreatedComplaintCount { get; private set; }

        public int CreatedCorrectionCount { get; private set; }

        public Task<JiraIssueDto> CreateComplaintAsync(
            Complaint complaint,
            CancellationToken cancellationToken = default)
        {
            CreatedComplaintCount++;

            var key = $"COMPLAINT-{_nextComplaintNumber++}";

            return Task.FromResult(new JiraIssueDto(key, "Complaint", "Open", $"mock://jira/{key}"));
        }

        public Task<JiraIssueDto> CreateCorrectionAsync(
            Complaint complaint,
            CancellationToken cancellationToken = default)
        {
            CreatedCorrectionCount++;

            var key = $"CORRECTION-{_nextCorrectionNumber++}";

            return Task.FromResult(new JiraIssueDto(key, "Correction", "Open", $"mock://jira/{key}"));
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
