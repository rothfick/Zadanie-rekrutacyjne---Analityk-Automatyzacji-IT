namespace Metalpol.Complaints.Domain.ValueObjects;

public sealed record JiraIssueRef
{
    public JiraIssueRef(string issueKey, string issueType, string? url = null)
    {
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            throw new ArgumentException("Jira issue key cannot be empty.", nameof(issueKey));
        }

        if (string.IsNullOrWhiteSpace(issueType))
        {
            throw new ArgumentException("Jira issue type cannot be empty.", nameof(issueType));
        }

        IssueKey = issueKey;
        IssueType = issueType;
        Url = url;
    }

    public string IssueKey { get; }

    public string IssueType { get; }

    public string? Url { get; }
}
