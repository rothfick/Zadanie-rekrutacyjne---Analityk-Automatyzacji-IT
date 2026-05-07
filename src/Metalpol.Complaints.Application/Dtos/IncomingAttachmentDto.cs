namespace Metalpol.Complaints.Application.Dtos;

public sealed record IncomingAttachmentDto
{
    public IncomingAttachmentDto(
        string attachmentId,
        string fileName,
        string contentType,
        long sizeBytes,
        byte[]? contentBytes = null)
    {
        DtoValidation.RequireNotBlank(attachmentId, nameof(attachmentId), "Attachment id is required.");
        DtoValidation.RequireNotBlank(fileName, nameof(fileName), "Attachment file name is required.");
        DtoValidation.RequireNotBlank(contentType, nameof(contentType), "Attachment content type is required.");
        DtoValidation.RequireNonNegative(sizeBytes, nameof(sizeBytes), "Attachment size cannot be negative.");

        AttachmentId = attachmentId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        ContentBytes = (contentBytes ?? Array.Empty<byte>()).ToArray();
    }

    public string AttachmentId { get; }

    public string FileName { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public byte[] ContentBytes { get; }
}
