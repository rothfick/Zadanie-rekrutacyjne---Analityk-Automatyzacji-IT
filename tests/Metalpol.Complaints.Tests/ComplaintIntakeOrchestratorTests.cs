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

public sealed class ComplaintIntakeOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HappyPathCreatesComplaintJiraIssueAndDraft()
    {
        var fixture = CreateFixture(TriageResult(confidence: 0.92m));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("message-happy"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.ResponseDrafted, result.Status);
        Assert.NotNull(complaint);
        Assert.Equal("COMPLAINT-1001", complaint.JiraComplaint?.IssueKey);
        Assert.Equal("Draft response.", complaint.ResponseDraft);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(JiraComplaintCreated));
        Assert.Contains(timeline, item => item.EventName == nameof(ResponseDrafted));
        Assert.DoesNotContain(timeline, item => item.EventName == nameof(HumanReviewRequested));
    }

    [Fact]
    public async Task MissingOrderRoutesToHumanReviewWithoutJira()
    {
        var fixture = CreateFixture(new AiTriageResult(
            "pl",
            orderNumber: null,
            description: "Brak numeru zamówienia.",
            DefectCategory.Unknown,
            0.50m,
            new[] { "orderNumber" },
            "Missing order number.",
            "Prosimy o podanie numeru zamówienia."));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("message-missing-order"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.True(complaint.AiTriage?.HasMissingFields);
        Assert.Equal("Prosimy o podanie numeru zamówienia.", complaint.ResponseDraft);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(ComplaintParsed));
        Assert.Contains(timeline, item => item.EventName == nameof(HumanReviewRequested));
    }

    [Fact]
    public async Task LowConfidenceRoutesToHumanReviewAfterJiraComplaint()
    {
        var fixture = CreateFixture(TriageResult(confidence: 0.70m));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("message-low-confidence"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Equal("COMPLAINT-1001", complaint.JiraComplaint?.IssueKey);
        Assert.Contains("AI confidence below threshold", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
    }

    [Fact]
    public async Task DuplicateEmailReturnsExistingComplaintWithoutSecondJiraIssue()
    {
        var fixture = CreateFixture(TriageResult(confidence: 0.92m));
        var email = CreateEmail("message-duplicate");

        var first = await fixture.Orchestrator.StartIntakeAsync(email);
        var second = await fixture.Orchestrator.StartIntakeAsync(email);
        var timeline = await fixture.EventLog.GetTimelineAsync(first.ComplaintId);

        Assert.Equal(first.ComplaintId, second.ComplaintId);
        Assert.Equal(ComplaintStatus.ResponseDrafted, second.Status);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(DuplicateLinked));
    }

    [Fact]
    public async Task SapOrderNotFoundRoutesToHumanReviewWithoutJira()
    {
        var fixture = CreateFixture(TriageResult(orderNumber: "UNKNOWN-ORDER", batchNumber: null, confidence: 0.92m));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("message-sap-not-found"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.Contains("SAP order not found", complaint.HumanReviewReason);
        Assert.Equal(0, fixture.Jira.CreatedComplaintCount);
        Assert.Contains(timeline, item => item.EventName == nameof(SapMismatchDetected));
    }

    [Fact]
    public async Task PromptInjectionRoutesToHumanReview()
    {
        var fixture = CreateFixture(TriageResult(confidence: 0.45m, promptInjectionDetected: true));

        var result = await fixture.Orchestrator.StartIntakeAsync(CreateEmail("message-prompt-injection"));
        var complaint = await fixture.Repository.GetByIdAsync(result.ComplaintId);

        Assert.Equal(ComplaintStatus.HumanReviewRequired, result.Status);
        Assert.NotNull(complaint);
        Assert.True(complaint.AiTriage?.PromptInjectionDetected);
        Assert.Contains("Prompt injection pattern detected", complaint.HumanReviewReason);
        Assert.Equal(1, fixture.Jira.CreatedComplaintCount);
    }

    private static TestFixture CreateFixture(AiTriageResult triageResult)
    {
        var repository = new InMemoryComplaintRepository();
        var eventLog = new InMemoryEventLog();
        var jira = new RecordingJiraClient();
        var orchestrator = new ComplaintIntakeOrchestrator(
            repository,
            eventLog,
            new FakeBlobStorageClient(),
            new StaticAiTriageService(triageResult),
            new FakeCustomerLookupService(),
            new FakeSapClient(),
            jira,
            new FixedClock(Now));

        return new TestFixture(orchestrator, repository, eventLog, jira);
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
        string orderNumber = "MP-2026-1042",
        string? batchNumber = "B-2026-07-19",
        decimal confidence = 0.92m,
        bool promptInjectionDetected = false)
    {
        return new AiTriageResult(
            "en",
            orderNumber,
            "Visual scratches on part.",
            DefectCategory.Visual,
            confidence,
            Array.Empty<string>(),
            "Visual complaint summary.",
            "Draft response.",
            batchNumber,
            promptInjectionDetected);
    }

    private sealed record TestFixture(
        ComplaintIntakeOrchestrator Orchestrator,
        InMemoryComplaintRepository Repository,
        InMemoryEventLog EventLog,
        RecordingJiraClient Jira);

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

    private sealed class RecordingJiraClient : IJiraClient
    {
        private int _nextComplaintNumber = 1001;

        public int CreatedComplaintCount { get; private set; }

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
