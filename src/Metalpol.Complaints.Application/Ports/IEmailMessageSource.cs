using Metalpol.Complaints.Application.Dtos;

namespace Metalpol.Complaints.Application.Ports;

public interface IEmailMessageSource
{
    Task<IncomingEmailDto?> GetMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IncomingEmailDto>> GetPendingMessagesAsync(
        CancellationToken cancellationToken = default);
}
