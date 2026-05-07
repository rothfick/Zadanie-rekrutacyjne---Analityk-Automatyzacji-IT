using Metalpol.Complaints.Application.Ports;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
