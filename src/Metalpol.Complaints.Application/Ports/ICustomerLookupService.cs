using Metalpol.Complaints.Application.Dtos;

namespace Metalpol.Complaints.Application.Ports;

public interface ICustomerLookupService
{
    Task<CustomerMatchDto> MatchByEmailAsync(
        string emailAddress,
        string? customerIdHint = null,
        CancellationToken cancellationToken = default);
}
