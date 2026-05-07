namespace Metalpol.Complaints.Application.Dtos;

public sealed record ResponseDraftDto
{
    public ResponseDraftDto(
        string language,
        string body,
        string? subject = null,
        bool requiresHumanReview = true,
        IReadOnlyCollection<string>? reviewReasons = null)
    {
        DtoValidation.RequireNotBlank(language, nameof(language), "Draft language is required.");
        DtoValidation.RequireNotBlank(body, nameof(body), "Draft body is required.");

        var reasons = DtoValidation.CopyStrings(reviewReasons);
        if (requiresHumanReview && reasons.Count == 0)
        {
            throw new ArgumentException("Human review draft requires at least one review reason.", nameof(reviewReasons));
        }

        Language = language;
        Body = body;
        Subject = string.IsNullOrWhiteSpace(subject) ? null : subject;
        RequiresHumanReview = requiresHumanReview;
        ReviewReasons = reasons;
    }

    public string Language { get; }

    public string Body { get; }

    public string? Subject { get; }

    public bool RequiresHumanReview { get; }

    public IReadOnlyCollection<string> ReviewReasons { get; }
}
