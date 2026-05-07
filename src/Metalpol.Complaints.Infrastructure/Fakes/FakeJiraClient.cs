using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.Entities;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class FakeJiraClient : IJiraClient
{
    private readonly int _firstComplaintNumber;
    private readonly int _firstCorrectionNumber;
    private int _nextComplaintNumber;
    private int _nextCorrectionNumber;

    public FakeJiraClient(
        int firstComplaintNumber = 1001,
        int firstCorrectionNumber = 2001)
    {
        _firstComplaintNumber = firstComplaintNumber;
        _firstCorrectionNumber = firstCorrectionNumber;
        _nextComplaintNumber = firstComplaintNumber;
        _nextCorrectionNumber = firstCorrectionNumber;
    }

    public Task<JiraIssueDto> CreateComplaintAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(complaint);

        var issueKey = $"COMPLAINT-{Next(ref _nextComplaintNumber)}";

        return Task.FromResult(new JiraIssueDto(
            issueKey,
            "Complaint",
            "Open",
            $"mock://jira/{issueKey}"));
    }

    public Task<JiraIssueDto> CreateCorrectionAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(complaint);

        var issueKey = $"CORRECTION-{Next(ref _nextCorrectionNumber)}";

        return Task.FromResult(new JiraIssueDto(
            issueKey,
            "Correction",
            "Open",
            $"mock://jira/{issueKey}"));
    }

    public Task<JiraIssueDto> UpdateIssueAsync(
        string issueKey,
        string status,
        IReadOnlyDictionary<string, string>? fields = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            throw new ArgumentException("Jira issue key is required.", nameof(issueKey));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Jira status is required.", nameof(status));
        }

        return Task.FromResult(new JiraIssueDto(issueKey, DetectIssueType(issueKey), status, $"mock://jira/{issueKey}"));
    }

    private static int Next(ref int number)
    {
        return Interlocked.Increment(ref number) - 1;
    }

    private static string DetectIssueType(string issueKey)
    {
        return issueKey.StartsWith("CORRECTION-", StringComparison.OrdinalIgnoreCase)
            ? "Correction"
            : "Complaint";
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _nextComplaintNumber, _firstComplaintNumber);
        Interlocked.Exchange(ref _nextCorrectionNumber, _firstCorrectionNumber);
    }
}
