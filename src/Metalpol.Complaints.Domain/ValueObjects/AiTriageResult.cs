using Metalpol.Complaints.Domain.Enums;

namespace Metalpol.Complaints.Domain.ValueObjects;

public sealed record AiTriageResult
{
    public AiTriageResult(
        string language,
        string? orderNumber,
        string? description,
        DefectCategory defectCategory,
        decimal confidenceScore,
        IReadOnlyCollection<string>? missingFields = null,
        string? summaryForSpecialist = null,
        string? customerResponseDraft = null,
        string? batchNumber = null,
        bool promptInjectionDetected = false)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("Language cannot be empty.", nameof(language));
        }

        if (confidenceScore is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence score must be between 0 and 1.");
        }

        Language = language;
        OrderNumber = string.IsNullOrWhiteSpace(orderNumber) ? null : orderNumber;
        BatchNumber = string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        DefectCategory = defectCategory;
        ConfidenceScore = confidenceScore;
        MissingFields = (missingFields ?? Array.Empty<string>()).ToArray();
        SummaryForSpecialist = summaryForSpecialist;
        CustomerResponseDraft = customerResponseDraft;
        PromptInjectionDetected = promptInjectionDetected;
    }

    public string Language { get; }

    public string DetectedLanguage => Language;

    public string? OrderNumber { get; }

    public string? BatchNumber { get; }

    public string? Description { get; }

    public string? DefectDescription => Description;

    public DefectCategory DefectCategory { get; }

    public DefectCategory ProposedCategory => DefectCategory;

    public decimal ConfidenceScore { get; }

    public decimal Confidence => ConfidenceScore;

    public IReadOnlyCollection<string> MissingFields { get; }

    public string? SummaryForSpecialist { get; }

    public string? Summary => SummaryForSpecialist;

    public string? CustomerResponseDraft { get; }

    public bool PromptInjectionDetected { get; }

    public bool HasMissingFields => MissingFields.Count > 0;
}
