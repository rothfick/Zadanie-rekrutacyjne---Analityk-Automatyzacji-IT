namespace Metalpol.Complaints.Application.Dtos;

public sealed record SapBatchDto
{
    public SapBatchDto(
        string batchId,
        bool exists,
        string? orderId = null,
        string? productionLine = null,
        DateOnly? productionDate = null)
    {
        DtoValidation.RequireNotBlank(batchId, nameof(batchId), "Batch id is required.");

        BatchId = batchId;
        Exists = exists;
        OrderId = string.IsNullOrWhiteSpace(orderId) ? null : orderId;
        ProductionLine = string.IsNullOrWhiteSpace(productionLine) ? null : productionLine;
        ProductionDate = productionDate;
    }

    public string BatchId { get; }

    public bool Exists { get; }

    public string? OrderId { get; }

    public string? ProductionLine { get; }

    public DateOnly? ProductionDate { get; }
}
