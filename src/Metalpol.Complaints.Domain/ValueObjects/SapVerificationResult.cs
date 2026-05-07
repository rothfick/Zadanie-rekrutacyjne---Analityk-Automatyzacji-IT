namespace Metalpol.Complaints.Domain.ValueObjects;

public sealed record SapVerificationResult
{
    private SapVerificationResult(
        bool isVerified,
        string? orderId,
        string? batchId,
        string? productionLine,
        string? failureReason)
    {
        IsVerified = isVerified;
        OrderId = orderId;
        BatchId = batchId;
        ProductionLine = productionLine;
        FailureReason = failureReason;
    }

    public bool IsVerified { get; }

    public string? OrderId { get; }

    public string? BatchId { get; }

    public string? ProductionLine { get; }

    public string? FailureReason { get; }

    public static SapVerificationResult Verified(
        string orderId,
        string? batchId = null,
        string? productionLine = null)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
        }

        return new SapVerificationResult(true, orderId, batchId, productionLine, null);
    }

    public static SapVerificationResult Failed(string failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("Failure reason cannot be empty.", nameof(failureReason));
        }

        return new SapVerificationResult(false, null, null, null, failureReason);
    }
}
