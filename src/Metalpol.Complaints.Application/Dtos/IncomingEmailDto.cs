namespace Metalpol.Complaints.Application.Dtos;

public sealed record IncomingEmailDto
{
    public IncomingEmailDto(
        string messageId,
        string fromEmail,
        string subject,
        string body,
        DateTimeOffset receivedAt,
        IReadOnlyCollection<IncomingAttachmentDto>? attachments = null)
    {
        DtoValidation.RequireNotBlank(messageId, nameof(messageId), "Message id is required.");
        DtoValidation.RequireNotBlank(fromEmail, nameof(fromEmail), "Sender email is required.");
        DtoValidation.RequireNotBlank(body, nameof(body), "Email body is required.");

        MessageId = messageId;
        FromEmail = fromEmail;
        Subject = subject ?? string.Empty;
        Body = body;
        ReceivedAt = receivedAt;
        Attachments = (attachments ?? Array.Empty<IncomingAttachmentDto>()).ToArray();
    }

    public string MessageId { get; }

    public string FromEmail { get; }

    public string Subject { get; }

    public string Body { get; }

    public DateTimeOffset ReceivedAt { get; }

    public IReadOnlyCollection<IncomingAttachmentDto> Attachments { get; }
}
