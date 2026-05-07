namespace Metalpol.Complaints.Application.Dtos;

public sealed record SapOrderDto
{
    public SapOrderDto(
        string orderId,
        bool exists,
        string? customerId = null,
        string? batchId = null,
        string? productionLine = null,
        string? status = null)
    {
        DtoValidation.RequireNotBlank(orderId, nameof(orderId), "Order id is required.");

        OrderId = orderId;
        Exists = exists;
        CustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId;
        BatchId = string.IsNullOrWhiteSpace(batchId) ? null : batchId;
        ProductionLine = string.IsNullOrWhiteSpace(productionLine) ? null : productionLine;
        Status = string.IsNullOrWhiteSpace(status) ? null : status;
    }

    public string OrderId { get; }

    public bool Exists { get; }

    public string? CustomerId { get; }

    public string? BatchId { get; }

    public string? ProductionLine { get; }

    public string? Status { get; }
}
