namespace Metalpol.Complaints.Application.Dtos;

public sealed record JiraIssueDto
{
    public JiraIssueDto(
        string issueKey,
        string issueType,
        string status,
        string? url = null)
    {
        DtoValidation.RequireNotBlank(issueKey, nameof(issueKey), "Jira issue key is required.");
        DtoValidation.RequireNotBlank(issueType, nameof(issueType), "Jira issue type is required.");
        DtoValidation.RequireNotBlank(status, nameof(status), "Jira issue status is required.");

        IssueKey = issueKey;
        IssueType = issueType;
        Status = status;
        Url = string.IsNullOrWhiteSpace(url) ? null : url;
    }

    public string IssueKey { get; }

    public string IssueType { get; }

    public string Status { get; }

    public string? Url { get; }
}
