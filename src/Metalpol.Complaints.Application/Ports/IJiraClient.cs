using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.Entities;

namespace Metalpol.Complaints.Application.Ports;

public interface IJiraClient
{
    Task<JiraIssueDto> CreateComplaintAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default);

    Task<JiraIssueDto> CreateCorrectionAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default);

    Task<JiraIssueDto> UpdateIssueAsync(
        string issueKey,
        string status,
        IReadOnlyDictionary<string, string>? fields = null,
        CancellationToken cancellationToken = default);
}
