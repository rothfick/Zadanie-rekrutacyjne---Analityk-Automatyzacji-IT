using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Application.Ports;

namespace Metalpol.Complaints.Infrastructure.Fakes;

public sealed class FakeCustomerLookupService : ICustomerLookupService
{
    private readonly IReadOnlyCollection<CustomerSample> _customers;

    public FakeCustomerLookupService()
        : this(SampleData.LoadArray<CustomerSample>("customers/customers.json"))
    {
    }

    public FakeCustomerLookupService(IReadOnlyCollection<CustomerSample> customers)
    {
        _customers = customers;
    }

    public Task<CustomerMatchDto> MatchByEmailAsync(
        string emailAddress,
        string? customerIdHint = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            throw new ArgumentException("Email address is required.", nameof(emailAddress));
        }

        var domain = ExtractDomain(emailAddress);
        var customer = FindCustomer(emailAddress, domain, customerIdHint);

        if (customer is null)
        {
            return Task.FromResult(new CustomerMatchDto(
                isMatched: false,
                customerId: null,
                displayName: null,
                domain,
                confidenceScore: 0m));
        }

        var confidence = Matches(emailAddress, customer.PrimaryEmail) || Matches(customerIdHint, customer.CustomerId)
            ? 1.00m
            : 0.90m;

        return Task.FromResult(new CustomerMatchDto(
            isMatched: true,
            customer.CustomerId,
            customer.DisplayName,
            customer.EmailDomain,
            confidence));
    }

    private CustomerSample? FindCustomer(string emailAddress, string? domain, string? customerIdHint)
    {
        if (!string.IsNullOrWhiteSpace(customerIdHint))
        {
            var byId = _customers.FirstOrDefault(customer => Matches(customer.CustomerId, customerIdHint));

            if (byId is not null)
            {
                return byId;
            }
        }

        return _customers.FirstOrDefault(customer =>
            Matches(customer.PrimaryEmail, emailAddress)
            || (!string.IsNullOrWhiteSpace(domain) && Matches(customer.EmailDomain, domain)));
    }

    private static string? ExtractDomain(string emailAddress)
    {
        var atIndex = emailAddress.LastIndexOf('@');

        return atIndex >= 0 && atIndex < emailAddress.Length - 1
            ? emailAddress[(atIndex + 1)..].ToLowerInvariant()
            : null;
    }

    private static bool Matches(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record CustomerSample(
    string CustomerId,
    string DisplayName,
    string PrimaryEmail,
    string EmailDomain);
