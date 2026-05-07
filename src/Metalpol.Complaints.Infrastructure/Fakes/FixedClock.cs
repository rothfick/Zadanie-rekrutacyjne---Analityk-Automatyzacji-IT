using Metalpol.Complaints.Application.Ports;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan time)
    {
        UtcNow = UtcNow.Add(time);
    }
}
