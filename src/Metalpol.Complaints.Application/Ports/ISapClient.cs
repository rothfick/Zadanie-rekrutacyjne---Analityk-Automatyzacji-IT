using Metalpol.Complaints.Application.Dtos;

namespace Metalpol.Complaints.Application.Ports;

public interface ISapClient
{
    Task<SapOrderDto> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    Task<SapBatchDto> GetBatchAsync(
        string batchId,
        CancellationToken cancellationToken = default);
}
