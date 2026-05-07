using System.Text.Json;

namespace Metalpol.Complaints.Infrastructure.Fakes;

internal static class SampleData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyCollection<T> LoadArray<T>(string sampleRelativePath)
    {
        var path = ResolvePath(sampleRelativePath);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<IReadOnlyCollection<T>>(json, JsonOptions)
            ?? Array.Empty<T>();
    }

    private static string ResolvePath(string sampleRelativePath)
    {
        var checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var startDirectory in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);

            while (directory is not null && checkedDirectories.Add(directory.FullName))
            {
                var candidate = Path.Combine(directory.FullName, "samples", sampleRelativePath);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Sample data file not found: samples/{sampleRelativePath}");
    }
}
