using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.ValueObjects;

namespace Metalpol.Complaints.Application.Ports;

public interface IBlobStorageClient
{
    Task<IReadOnlyCollection<AttachmentInfo>> StoreAttachmentsAsync(
        ComplaintId complaintId,
        IReadOnlyCollection<IncomingAttachmentDto> attachments,
        CancellationToken cancellationToken = default);
}
