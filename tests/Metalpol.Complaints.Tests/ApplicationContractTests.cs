using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Infrastructure.Fakes;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ApplicationContractTests
{
    [Fact]
    public void IncomingEmailRequiresMessageId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new IncomingEmailDto(
                string.Empty,
                "customer@example.com",
                "Complaint",
                "Part has visible scratches.",
                DateTimeOffset.UtcNow));

        Assert.Equal("messageId", exception.ParamName);
    }

    [Fact]
    public void IncomingAttachmentRejectsNegativeSize()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new IncomingAttachmentDto(
                "att-001",
                "photo.jpg",
                "image/jpeg",
                -1));

        Assert.Equal("sizeBytes", exception.ParamName);
    }

    [Fact]
    public void MatchedCustomerRequiresCustomerId()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new CustomerMatchDto(
                isMatched: true,
                customerId: null,
                displayName: "Example Customer",
                emailDomain: "example.com",
                confidenceScore: 0.90m));

        Assert.Equal("customerId", exception.ParamName);
    }

    [Fact]
    public void DashboardKpisRejectInvalidPercentValues()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new DashboardKpisDto(
                TimeSpan.FromMinutes(2),
                TimeSpan.FromHours(4),
                TimeSpan.FromDays(2),
                backlogSize: 3,
                slaBreachCount: 1,
                new Dictionary<DefectCategory, int> { [DefectCategory.Visual] = 4 },
                new Dictionary<string, int> { ["LINE-1"] = 2 },
                new Dictionary<string, int> { ["BATCH-1"] = 1 },
                manualReviewRatePercent: 101,
                new Dictionary<string, int> { ["0.85-1.00"] = 5 },
                classificationCorrectionRatePercent: 3,
                jiraIssueCreationSuccessRatePercent: 99,
                sapVerificationFailureRatePercent: 4));

        Assert.Equal("manualReviewRatePercent", exception.ParamName);
    }

    [Fact]
    public async Task FakeAiTriageReturnsDeterministicMissingOrderResult()
    {
        var triage = new FakeAiTriageService();
        var email = new IncomingEmailDto(
            "message-001",
            "customer@example.com",
            "Reklamacja",
            "Na elemencie widoczna jest rysa.",
            DateTimeOffset.UtcNow);

        var result = await triage.ExtractAsync(email);

        Assert.Equal("pl", result.Language);
        Assert.Equal(DefectCategory.Visual, result.DefectCategory);
        Assert.Equal(0.55m, result.ConfidenceScore);
        Assert.Contains("orderNumber", result.MissingFields);
    }
}
