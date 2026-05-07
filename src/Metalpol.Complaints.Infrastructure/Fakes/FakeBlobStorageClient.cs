using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class FakeBlobStorageClient : IBlobStorageClient
{
    public Task<IReadOnlyCollection<AttachmentInfo>> StoreAttachmentsAsync(
        ComplaintId complaintId,
        IReadOnlyCollection<IncomingAttachmentDto> attachments,
        CancellationToken cancellationToken = default)
    {
        if (attachments.Count == 0)
        {
            throw new ArgumentException("At least one attachment is required.", nameof(attachments));
        }

        var stored = attachments
            .Select(attachment => new AttachmentInfo(
                attachment.AttachmentId,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeBytes,
                BuildFakeBlobUrl(complaintId, attachment)))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<AttachmentInfo>>(stored);
    }

    private static string BuildFakeBlobUrl(ComplaintId complaintId, IncomingAttachmentDto attachment)
    {
        return "https://mock.blob.local/complaints/"
            + $"{Uri.EscapeDataString(complaintId.Value)}/"
            + $"{Uri.EscapeDataString(attachment.AttachmentId)}/"
            + Uri.EscapeDataString(attachment.FileName);
    }
}
