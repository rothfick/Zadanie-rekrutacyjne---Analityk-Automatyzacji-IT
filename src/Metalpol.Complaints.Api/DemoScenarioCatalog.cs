namespace Metalpol.Complaints.Api;

internal static class DemoScenarioCatalog
{
    private static readonly IReadOnlyDictionary<string, ScenarioMetadata> Metadata =
        new Dictionary<string, ScenarioMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["happy-path-visual-defect"] = new(
                10,
                "Happy path: wada wizualna",
                "Poprawny mail z reklamacją wizualną, orderem, batchem i zdjęciami.",
                "Pokazuje standardowy pipeline: email intake, AI triage, SAP ERP, Jira Cloud i draft odpowiedzi."),
            ["missing-order-number"] = new(
                20,
                "Brak numeru zamówienia",
                "Mail bez numeru zamówienia.",
                "AI nie zgaduje danych krytycznych i kieruje sprawę do human review."),
            ["dimensional-defect-low-confidence"] = new(
                30,
                "Wada wymiarowa: niska pewność",
                "Reklamacja wymiarowa z niższą pewnością klasyfikacji.",
                "Niska pewność uruchamia kontrolę specjalisty."),
            ["sap-order-not-found"] = new(
                40,
                "SAP ERP: brak orderu",
                "Mail zawiera order, którego mock SAP ERP nie znajduje.",
                "Walidacja SAP blokuje automatyczne przejście do standardowego flow."),
            ["prompt-injection-attempt"] = new(
                50,
                "Prompt injection: podejrzana instrukcja",
                "Treść maila zawiera podejrzaną instrukcję dla modelu.",
                "Wejście klienta jest niezaufane i wymaga review."),
            ["duplicate-message"] = new(
                60,
                "Duplikat maila po happy path",
                "Powtórzony sourceMessageId dla scenariusza happy path.",
                "Najpierw uruchom happy path, potem ten scenariusz. Idempotencja chroni przed drugim Jira Complaint."),
            ["logistics-complaint"] = new(
                70,
                "Reklamacja logistyczna",
                "Reklamacja dotycząca dostawy lub błędnej pozycji.",
                "Kontrolowana taksonomia oddziela logistykę od wad jakościowych."),
            ["material-defect-requires-correction"] = new(
                80,
                "Wada materiałowa: Correction",
                "Reklamacja materiałowa gotowa do decyzji człowieka.",
                "Correction powstaje dopiero po zatwierdzeniu przez specjalistę.")
        };

    public static IReadOnlyCollection<DemoScenarioDescriptor> List()
    {
        var directory = ResolveScenarioDirectory();
        if (directory is null)
        {
            return Array.Empty<DemoScenarioDescriptor>();
        }

        return Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(CreateDescriptor)
            .OrderBy(scenario => scenario.SortOrder)
            .ThenBy(scenario => scenario.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? ReadScenarioJson(string id)
    {
        var descriptor = List().FirstOrDefault(
            scenario => string.Equals(scenario.Id, id, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null)
        {
            return null;
        }

        var directory = ResolveScenarioDirectory();
        if (directory is null)
        {
            return null;
        }

        var path = Path.Combine(directory, descriptor.FileName);

        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static DemoScenarioDescriptor CreateDescriptor(string path)
    {
        var fileName = Path.GetFileName(path);
        var id = Path.GetFileNameWithoutExtension(fileName);
        var metadata = Metadata.TryGetValue(id, out var known)
            ? known
            : new ScenarioMetadata(1000, id, "Demo scenario loaded from samples/scenarios.", "Pokazuje wybrany wariant procesu reklamacji.");

        return new DemoScenarioDescriptor(id, fileName, metadata.Label, metadata.Description, metadata.BusinessCase, metadata.SortOrder);
    }

    private static string? ResolveScenarioDirectory()
    {
        foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "samples", "scenarios");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private sealed record ScenarioMetadata(int SortOrder, string Label, string Description, string BusinessCase);
}

public sealed record DemoScenarioDescriptor(
    string Id,
    string FileName,
    string Label,
    string Description,
    string BusinessCase,
    int SortOrder);
