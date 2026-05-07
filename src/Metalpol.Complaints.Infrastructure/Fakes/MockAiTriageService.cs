using System.Text.RegularExpressions;
using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public class MockAiTriageService : IAiTriageService
{
    private static readonly Regex OrderWithPrefixPattern = new(
        @"\b(?:ORDER|ZAM)-\d+\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OrderWithLabelPattern = new(
        @"\border\s*[:#-]?\s*(\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BatchWithPrefixPattern = new(
        @"\b(?:BATCH|PARTIA|LOT)-[A-Z0-9-]+\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BatchWithLabelPattern = new(
        @"\b(?:batch|partia|lot)\s*[:#-]?\s*([A-Z0-9-]+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] PromptInjectionPhrases =
    {
        "ignore previous instructions",
        "disregard previous instructions",
        "forget previous instructions",
        "ignore all previous instructions",
        "system prompt",
        "you are now",
        "jailbreak"
    };

    public Task<AiTriageResult> ExtractAsync(
        IncomingEmailDto email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        var text = $"{email.Subject}\n{email.Body}";
        var detectedLanguage = DetectLanguage(text);
        var orderNumber = ExtractOrderNumber(text);
        var batchNumber = ExtractBatchNumber(text);
        var category = Classify(text);
        var promptInjectionDetected = ContainsAny(text, PromptInjectionPhrases);
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            missingFields.Add("orderNumber");
        }

        var confidence = CalculateConfidence(text, orderNumber, category, promptInjectionDetected);

        var result = new AiTriageResult(
            detectedLanguage,
            orderNumber,
            email.Body,
            category,
            confidence,
            missingFields,
            BuildSummary(email, detectedLanguage, orderNumber, batchNumber, category, promptInjectionDetected),
            BuildCustomerResponseDraft(detectedLanguage, missingFields.Count > 0),
            batchNumber,
            promptInjectionDetected);

        return Task.FromResult(result);
    }

    public Task<ResponseDraftDto> DraftResponseAsync(
        Complaint complaint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(complaint);

        var draft = new ResponseDraftDto(
            "pl",
            "Dziękujemy za zgłoszenie reklamacyjne. Sprawdzimy dane zamówienia i partii, a następnie wrócimy z informacją po weryfikacji.",
            "Potwierdzenie przyjęcia reklamacji",
            requiresHumanReview: true,
            new[] { "Customer response requires service specialist approval." });

        return Task.FromResult(draft);
    }

    private static string? ExtractOrderNumber(string text)
    {
        var prefixed = OrderWithPrefixPattern.Match(text);

        if (prefixed.Success)
        {
            return prefixed.Value.ToUpperInvariant();
        }

        var labeled = OrderWithLabelPattern.Match(text);

        return labeled.Success ? labeled.Groups[1].Value : null;
    }

    private static string? ExtractBatchNumber(string text)
    {
        var prefixed = BatchWithPrefixPattern.Match(text);

        if (prefixed.Success)
        {
            return prefixed.Value.ToUpperInvariant();
        }

        var labeled = BatchWithLabelPattern.Match(text);

        return labeled.Success ? labeled.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static string DetectLanguage(string text)
    {
        var hasPolishSignal = ContainsAny(
            text,
            "reklamacja",
            "reklamację",
            "zamówienie",
            "zamowienie",
            "partia",
            "rysa",
            "wymiar",
            "dostawa",
            "brak",
            "paczka",
            "pęknięcie",
            "pekniecie",
            "lakier",
            "element")
            || text.Any(character => "ąćęłńóśźżĄĆĘŁŃÓŚŹŻ".Contains(character));

        var hasEnglishSignal = ContainsAny(
            text,
            "complaint",
            "order",
            "batch",
            "scratches",
            "scratch",
            "dimension",
            "tolerance",
            "delivery",
            "package",
            "wrong item",
            "missing",
            "crack",
            "hardness",
            "dent",
            "paint");

        return (hasPolishSignal, hasEnglishSignal) switch
        {
            (true, true) => "mixed",
            (true, false) => "pl",
            _ => "en"
        };
    }

    private static DefectCategory Classify(string text)
    {
        if (ContainsAny(text, "scratches", "scratch", "paint", "dent", "rysa", "lakier", "wgniecenie"))
        {
            return DefectCategory.Visual;
        }

        if (ContainsAny(text, "dimension", "tolerance", "size", "mm", "wymiar", "tolerancja", "rozmiar"))
        {
            return DefectCategory.Dimensional;
        }

        if (ContainsAny(text, "material", "crack", "hardness", "materiał", "material", "pęknięcie", "pekniecie", "twardość", "twardosc"))
        {
            return DefectCategory.Material;
        }

        if (ContainsAny(text, "delivery", "missing", "wrong item", "package", "dostawa", "brak", "zły element", "zly element", "paczka"))
        {
            return DefectCategory.Logistics;
        }

        return DefectCategory.Unknown;
    }

    private static decimal CalculateConfidence(
        string text,
        string? orderNumber,
        DefectCategory category,
        bool promptInjectionDetected)
    {
        var confidence = string.IsNullOrWhiteSpace(orderNumber)
            ? 0.55m
            : category == DefectCategory.Unknown ? 0.65m : 0.90m;

        if (string.IsNullOrWhiteSpace(orderNumber) && category == DefectCategory.Unknown)
        {
            confidence = 0.50m;
        }

        if (ContainsAny(text, "unclear", "not sure", "maybe", "possibly", "trudno ocenić", "niepewne", "słabe zdjęcie"))
        {
            confidence = Math.Min(confidence, 0.70m);
        }

        if (promptInjectionDetected)
        {
            confidence = Math.Min(confidence, 0.45m);
        }

        return confidence;
    }

    private static string BuildSummary(
        IncomingEmailDto email,
        string detectedLanguage,
        string? orderNumber,
        string? batchNumber,
        DefectCategory category,
        bool promptInjectionDetected)
    {
        var orderText = string.IsNullOrWhiteSpace(orderNumber) ? "order missing" : $"order {orderNumber}";
        var batchText = string.IsNullOrWhiteSpace(batchNumber) ? "batch not provided" : $"batch {batchNumber}";
        var securityText = promptInjectionDetected ? " Prompt injection pattern detected; route to human review." : string.Empty;

        return $"Language {detectedLanguage}; {orderText}; {batchText}; proposed category {category}; sender {email.FromEmail}.{securityText}";
    }

    private static string BuildCustomerResponseDraft(string detectedLanguage, bool missingOrderNumber)
    {
        if (detectedLanguage == "en")
        {
            return missingOrderNumber
                ? "Thank you for your complaint. Please provide the order number so we can continue verification."
                : "Thank you for your complaint. We received it and will verify the order and batch data before sending the final response.";
        }

        return missingOrderNumber
            ? "Dziękujemy za zgłoszenie reklamacyjne. Prosimy o podanie numeru zamówienia, aby kontynuować weryfikację."
            : "Dziękujemy za zgłoszenie reklamacyjne. Przyjęliśmy je do weryfikacji danych zamówienia i partii.";
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
