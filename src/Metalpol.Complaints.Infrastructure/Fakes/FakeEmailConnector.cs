using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class FakeEmailConnector : IEmailMessageSource
{
    private readonly List<IncomingEmailDto> _messages = new();

    public FakeEmailConnector()
    {
    }

    public FakeEmailConnector(IEnumerable<IncomingEmailDto> messages)
    {
        _messages.AddRange(messages);
    }

    public void Add(IncomingEmailDto email)
    {
        ArgumentNullException.ThrowIfNull(email);

        _messages.Add(email);
    }

    public Task<IncomingEmailDto?> GetMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        var message = _messages.FirstOrDefault(email => email.MessageId == messageId);

        return Task.FromResult(message);
    }

    public Task<IReadOnlyCollection<IncomingEmailDto>> GetPendingMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<IncomingEmailDto>>(_messages.ToArray());
    }
}
