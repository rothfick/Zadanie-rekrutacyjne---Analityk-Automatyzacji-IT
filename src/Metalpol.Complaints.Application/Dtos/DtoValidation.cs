namespace Metalpol.Complaints.Application.Dtos;

internal static class DtoValidation
{
    public static void RequireNotBlank(string? value, string parameterName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }
    }

    public static void RequireNonNegative(int value, string parameterName, string message)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, message);
        }
    }

    public static void RequireNonNegative(long value, string parameterName, string message)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, message);
        }
    }

    public static void RequireNonNegative(TimeSpan value, string parameterName, string message)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, message);
        }
    }

    public static void RequireRatio(decimal value, string parameterName)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must be between 0 and 1.");
        }
    }

    public static void RequirePercent(decimal value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Percentage must be between 0 and 100.");
        }
    }

    public static IReadOnlyCollection<string> CopyStrings(IReadOnlyCollection<string>? values)
    {
        return (values ?? Array.Empty<string>()).ToArray();
    }

    public static IReadOnlyDictionary<TKey, int> CopyCountMap<TKey>(
        IReadOnlyDictionary<TKey, int>? values,
        string parameterName) where TKey : notnull
    {
        var copy = new Dictionary<TKey, int>(values ?? new Dictionary<TKey, int>());

        if (copy.Values.Any(value => value < 0))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Count map cannot contain negative values.");
        }

        return copy;
    }

    public static IReadOnlyDictionary<string, string> CopyStringMap(
        IReadOnlyDictionary<string, string>? values)
    {
        return new Dictionary<string, string>(values ?? new Dictionary<string, string>());
    }
}
