namespace Metalpol.Complaints.Application.Dtos;

public sealed record CustomerMatchDto
{
    public CustomerMatchDto(
        bool isMatched,
        string? customerId,
        string? displayName,
        string? emailDomain,
        decimal confidenceScore)
    {
        DtoValidation.RequireRatio(confidenceScore, nameof(confidenceScore));

        if (isMatched)
        {
            DtoValidation.RequireNotBlank(customerId, nameof(customerId), "Matched customer id is required.");
            DtoValidation.RequireNotBlank(displayName, nameof(displayName), "Matched customer display name is required.");
        }

        IsMatched = isMatched;
        CustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        EmailDomain = string.IsNullOrWhiteSpace(emailDomain) ? null : emailDomain;
        ConfidenceScore = confidenceScore;
    }

    public bool IsMatched { get; }

    public string? CustomerId { get; }

    public string? DisplayName { get; }

    public string? EmailDomain { get; }

    public decimal ConfidenceScore { get; }
}
