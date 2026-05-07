namespace Metalpol.Complaints.Domain.ValueObjects;

public sealed record AttachmentInfo
{
    public AttachmentInfo(
        string attachmentId,
        string fileName,
        string contentType,
        long sizeBytes,
        string? storageUri = null)
    {
        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            throw new ArgumentException("Attachment id cannot be empty.", nameof(attachmentId));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be empty.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));
        }

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Attachment size cannot be negative.");
        }

        AttachmentId = attachmentId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageUri = storageUri;
    }

    public string AttachmentId { get; }

    public string FileName { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public string? StorageUri { get; }
}
