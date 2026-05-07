using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class FakeSapClient : ISapClient
{
    private const string TimeoutOrderId = "SAP-TIMEOUT";
    private const string RateLimitOrderId = "SAP-RATE-LIMIT";

    private readonly IReadOnlyCollection<SapOrderSample> _orders;
    private readonly IReadOnlyCollection<SapBatchSample> _batches;

    public FakeSapClient()
        : this(
            SampleData.LoadArray<SapOrderSample>("sap/orders.json"),
            SampleData.LoadArray<SapBatchSample>("sap/batches.json"))
    {
    }

    public FakeSapClient(
        IReadOnlyCollection<SapOrderSample> orders,
        IReadOnlyCollection<SapBatchSample> batches)
    {
        _orders = orders;
        _batches = batches;
    }

    public Task<SapOrderDto> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order id is required.", nameof(orderId));
        }

        if (Matches(orderId, TimeoutOrderId))
        {
            throw new TimeoutException("Simulated SAP timeout.");
        }

        if (Matches(orderId, RateLimitOrderId))
        {
            throw new InvalidOperationException("Simulated SAP rate limit.");
        }

        var order = _orders.FirstOrDefault(item => Matches(item.OrderId, orderId));

        return Task.FromResult(order is null
            ? new SapOrderDto(orderId, exists: false)
            : new SapOrderDto(
                order.OrderId,
                order.Exists,
                order.CustomerId,
                order.BatchId,
                order.ProductionLine,
                order.Status));
    }

    public Task<SapBatchDto> GetBatchAsync(
        string batchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(batchId))
        {
            throw new ArgumentException("Batch id is required.", nameof(batchId));
        }

        var batch = _batches.FirstOrDefault(item => Matches(item.BatchId, batchId));

        return Task.FromResult(batch is null
            ? new SapBatchDto(batchId, exists: false)
            : new SapBatchDto(
                batch.BatchId,
                batch.Exists,
                batch.OrderId,
                batch.ProductionLine,
                batch.ProductionDate));
    }

    private static bool Matches(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record SapOrderSample(
    string OrderId,
    bool Exists,
    string? CustomerId,
    string? BatchId,
    string? ProductionLine,
    string? Status);

public sealed record SapBatchSample(
    string BatchId,
    bool Exists,
    string? OrderId,
    string? ProductionLine,
    DateOnly? ProductionDate);
