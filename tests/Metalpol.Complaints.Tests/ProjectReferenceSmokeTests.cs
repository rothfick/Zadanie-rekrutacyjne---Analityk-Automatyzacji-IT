using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.ValueObjects;
using Metalpol.Complaints.Infrastructure.Fakes;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ProjectReferenceSmokeTests
{
    [Fact]
    public void DomainAndInfrastructureAssembliesAreLoadable()
    {
        var complaint = Complaint.ReceiveEmail(new ComplaintId("CMP-TEST-001"), "message-001");

        Assert.Equal("CMP-TEST-001", complaint.Id.Value);
        Assert.Equal("Metalpol.Complaints.Infrastructure", typeof(FakeEmailConnector).Assembly.GetName().Name);
    }
}
