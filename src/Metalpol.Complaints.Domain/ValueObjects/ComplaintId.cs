namespace Metalpol.Complaints.Domain.ValueObjects;

public readonly record struct ComplaintId
{
    public ComplaintId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Complaint id cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
