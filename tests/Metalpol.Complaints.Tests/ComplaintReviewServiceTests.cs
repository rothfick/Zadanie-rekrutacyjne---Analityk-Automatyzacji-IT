using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Application.Review;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;
using Metalpol.Complaints.Infrastructure.Fakes;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ComplaintReviewServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConfirmedDefectCreatesCorrectionTicket()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.ApproveComplaintAsync(
            fixture.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.ConfirmDefect,
            "Defect confirmed by quality.");
        var complaint = await fixture.Repository.GetByIdAsync(fixture.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(fixture.ComplaintId);

        Assert.True(result.Succeeded);
        Assert.Equal(ComplaintStatus.CorrectionCreated, result.Status);
        Assert.Equal("CORRECTION-2001", result.CorrectionIssueKey);
        Assert.NotNull(complaint);
        Assert.Equal("CORRECTION-2001", complaint.CorrectionTicket?.IssueKey);
        Assert.Equal(1, fixture.Jira.CreatedCorrectionCount);
        Assert.Contains(timeline, item => item.EventName == nameof(HumanReviewCompleted));
        Assert.Contains(timeline, item => item.EventName == nameof(CorrectionTicketCreated));
    }

    [Fact]
    public async Task RequestMoreInfoDoesNotCreateCorrectionTicket()
    {
        var fixture = await CreateFixtureAsync();

        var result = await fixture.Service.ApproveComplaintAsync(
            fixture.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.RequestMoreInfo,
            "Please ask customer for clearer photos.");
        var complaint = await fixture.Repository.GetByIdAsync(fixture.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(fixture.ComplaintId);

        Assert.True(result.Succeeded);
        Assert.Equal(ComplaintStatus.MissingData, result.Status);
        Assert.NotNull(complaint);
        Assert.Null(complaint.CorrectionTicket);
        Assert.Equal(0, fixture.Jira.CreatedCorrectionCount);
        Assert.Contains(timeline, item => item.EventName == nameof(CustomerClarificationRequested));
        Assert.Contains(timeline, item => item.EventName == nameof(HumanReviewCompleted));
    }

    [Fact]
    public async Task InvalidComplaintIdReturnsClearError()
    {
        var fixture = await CreateFixtureAsync();
        var missingId = new ComplaintId("CMP-MISSING");

        var result = await fixture.Service.ApproveComplaintAsync(
            missingId,
            "service.specialist",
            ComplaintReviewDecision.ConfirmDefect,
            "Reviewed.");

        Assert.False(result.Succeeded);
        Assert.Equal(missingId, result.ComplaintId);
        Assert.Contains("CMP-MISSING", result.Error);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Jira.CreatedCorrectionCount);
    }

    [Fact]
    public async Task ConfirmedDefectApprovalIsIdempotent()
    {
        var fixture = await CreateFixtureAsync();

        var first = await fixture.Service.ApproveComplaintAsync(
            fixture.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.ConfirmDefect,
            "Defect confirmed.");
        var second = await fixture.Service.ApproveComplaintAsync(
            fixture.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.ConfirmDefect,
            "Second click.");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal("CORRECTION-2001", first.CorrectionIssueKey);
        Assert.Equal("CORRECTION-2001", second.CorrectionIssueKey);
        Assert.Equal(ComplaintStatus.CorrectionCreated, second.Status);
        Assert.Equal(1, fixture.Jira.CreatedCorrectionCount);
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var repository = new InMemoryComplaintRepository();
        var eventLog = new InMemoryEventLog();
        var jira = new RecordingJiraClient();
        var service = new ComplaintReviewService(
            repository,
            eventLog,
            jira,
            new FixedClock(Now));
        var complaint = CreateReviewReadyComplaint();

        complaint.ClearDomainEvents();
        await repository.SaveAsync(complaint);

        return new TestFixture(service, repository, eventLog, jira, complaint.Id);
    }

    private static Complaint CreateReviewReadyComplaint()
    {
        var complaint = Complaint.ReceiveEmail(new ComplaintId("CMP-REVIEW-001"), "message-review-001", Now);
        complaint.QueueIntake();
        complaint.Parse(new AiTriageResult(
            "en",
            "MP-2026-1042",
            "Visual scratches on part.",
            DefectCategory.Visual,
            0.90m));
        complaint.MatchCustomer("CUST-AUTOMOTIVE-PL", Now);
        complaint.VerifyOrder(SapVerificationResult.Verified("MP-2026-1042", "B-2026-07-19", "LINE-2"), Now);
        complaint.VerifyBatch(SapVerificationResult.Verified("MP-2026-1042", "B-2026-07-19", "LINE-2"), Now);
        complaint.CreateJiraComplaint(new JiraIssueRef("COMPLAINT-1001", "Complaint"), Now);
        complaint.DraftResponse("Draft response.", Now);
        complaint.RequestHumanReview("Review required before customer response.", Now);

        return complaint;
    }

    private sealed record TestFixture(
        ComplaintReviewService Service,
        InMemoryComplaintRepository Repository,
        InMemoryEventLog EventLog,
        RecordingJiraClient Jira,
        ComplaintId ComplaintId);

    private sealed class RecordingJiraClient : IJiraClient
    {
        private int _nextCorrectionNumber = 2001;

        public int CreatedCorrectionCount { get; private set; }

        public Task<JiraIssueDto> CreateComplaintAsync(
            Complaint complaint,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new JiraIssueDto("COMPLAINT-1001", "Complaint", "Open", "mock://jira/COMPLAINT-1001"));
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
