namespace Metalpol.Complaints.Application.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
