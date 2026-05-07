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

public sealed class ComplaintReviewEdgeCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 8, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task RejectComplaintClosesCaseWithoutCreatingCorrection()
    {
        var fixture = await CreateReviewFixtureAsync(CreateReviewReadyComplaint());

        var result = await fixture.Service.ApproveComplaintAsync(
            fixture.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.RejectComplaint,
            "Complaint rejected after review.");
        var complaint = await fixture.Repository.GetByIdAsync(fixture.ComplaintId);
        var timeline = await fixture.EventLog.GetTimelineAsync(fixture.ComplaintId);

        Assert.True(result.Succeeded);
        Assert.Equal(ComplaintStatus.Closed, result.Status);
        Assert.Null(result.CorrectionIssueKey);
        Assert.NotNull(complaint);
        Assert.Null(complaint.CorrectionTicket);
        Assert.Equal(0, fixture.Jira.CreatedCorrectionCount);
        Assert.Contains(timeline, item => item.EventName == nameof(HumanReviewCompleted));
        Assert.Contains(timeline, item => item.EventName == nameof(ComplaintClosed));
    }

    [Fact]
    public async Task ConfirmDefectBeforeDraftReturnsFailureAndDoesNotCreateCorrection()
    {
        var fixture = await CreateReviewFixtureAsync(CreateParsedComplaint());

        var result = await fixture.Service.ApproveComplaintAsync(
            fixture.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.ConfirmDefect,
            "Premature approval.");
        var complaint = await fixture.Repository.GetByIdAsync(fixture.ComplaintId);

        Assert.False(result.Succeeded);
        Assert.Contains("Customer response can only be approved", result.Error);
        Assert.NotNull(complaint);
        Assert.Equal(ComplaintStatus.Parsed, complaint.Status);
        Assert.Equal(0, fixture.Jira.CreatedCorrectionCount);
    }

    [Fact]
    public async Task ConfirmDefectOnClosedComplaintReturnsFailureAndDoesNotReopenCase()
    {
        var complaint = CreateReviewReadyComplaint();
        complaint.RejectAfterReview("service.specialist", "RejectComplaint", "Closed after review.", Now);
        var fixture = await CreateReviewFixtureAsync(complaint);

        var result = await fixture.Service.ApproveComplaintAsync(
            fixture.ComplaintId,
            "service.specialist",
            ComplaintReviewDecision.ConfirmDefect,
            "Late approval attempt.");
        var stored = await fixture.Repository.GetByIdAsync(fixture.ComplaintId);

        Assert.False(result.Succeeded);
        Assert.Contains("terminal complaint", result.Error);
        Assert.NotNull(stored);
        Assert.Equal(ComplaintStatus.Closed, stored.Status);
        Assert.Equal(0, fixture.Jira.CreatedCorrectionCount);
    }

    private static async Task<ReviewFixture> CreateReviewFixtureAsync(Complaint complaint)
    {
        var repository = new InMemoryComplaintRepository();
        var eventLog = new InMemoryEventLog();
        var jira = new RecordingJiraClient();
        var service = new ComplaintReviewService(repository, eventLog, jira, new FixedClock(Now));

        complaint.ClearDomainEvents();
        await repository.SaveAsync(complaint);

        return new ReviewFixture(service, repository, eventLog, jira, complaint.Id);
    }

    private static Complaint CreateReviewReadyComplaint()
    {
        var complaint = CreateParsedComplaint();

        complaint.MatchCustomer("CUST-AUTOMOTIVE-PL", Now);
        complaint.VerifyOrder(SapVerificationResult.Verified("ORDER-1001", "BATCH-1001", "LINE-1"), Now);
        complaint.VerifyBatch(SapVerificationResult.Verified("ORDER-1001", "BATCH-1001", "LINE-1"), Now);
        complaint.CreateJiraComplaint(new JiraIssueRef("COMPLAINT-1001", "Complaint", "mock://jira/COMPLAINT-1001"), Now);
        complaint.DraftResponse("Draft response.", Now);
        complaint.RequestHumanReview("Review required.", Now);

        return complaint;
    }

    private static Complaint CreateParsedComplaint()
    {
        var complaint = Complaint.ReceiveEmail(
            new ComplaintId($"CMP-REVIEW-EDGE-{Guid.NewGuid():N}"),
            $"message-review-edge-{Guid.NewGuid():N}",
            Now);

        complaint.QueueIntake();
        complaint.Parse(new AiTriageResult(
            "en",
            "ORDER-1001",
            "Visual scratches.",
            DefectCategory.Visual,
            0.90m));

        return complaint;
    }

    private sealed record ReviewFixture(
        ComplaintReviewService Service,
        InMemoryComplaintRepository Repository,
        InMemoryEventLog EventLog,
        RecordingJiraClient Jira,
        ComplaintId ComplaintId);

    private sealed class RecordingJiraClient : IJiraClient
    {
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
