using Metalpol.Complaints.Application.Dtos;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.ValueObjects;
using Metalpol.Complaints.Infrastructure.Fakes;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class InfrastructureFakeAdapterTests
{
    [Fact]
    public async Task CustomerLookupMatchesCustomerByEmailDomainFromSampleData()
    {
        var service = new FakeCustomerLookupService();

        var match = await service.MatchByEmailAsync("claim@automotive-pl.example");

        Assert.True(match.IsMatched);
        Assert.Equal("CUST-AUTOMOTIVE-PL", match.CustomerId);
        Assert.Equal("Automotive Components Poland", match.DisplayName);
        Assert.Equal(0.90m, match.ConfidenceScore);
    }

    [Fact]
    public async Task SapClientReturnsOrderAndBatchFromSampleData()
    {
        var sap = new FakeSapClient();

        var order = await sap.GetOrderAsync("MP-2026-1042");
        var batch = await sap.GetBatchAsync(order.BatchId!);

        Assert.True(order.Exists);
        Assert.Equal("B-2026-07-19", order.BatchId);
        Assert.Equal("LINE-2", order.ProductionLine);
        Assert.True(batch.Exists);
        Assert.Equal("MP-2026-1042", batch.OrderId);
    }

    [Fact]
    public async Task SapClientReturnsNotFoundForUnknownOrder()
    {
        var sap = new FakeSapClient();

        var order = await sap.GetOrderAsync("UNKNOWN-ORDER");

        Assert.False(order.Exists);
        Assert.Equal("UNKNOWN-ORDER", order.OrderId);
        Assert.Null(order.BatchId);
    }

    [Fact]
    public async Task JiraClientGeneratesDeterministicKeys()
    {
        var jira = new FakeJiraClient();
        var complaint = Complaint.ReceiveEmail(new ComplaintId("CMP-2026-0001"), "message-001");

        var firstComplaint = await jira.CreateComplaintAsync(complaint);
        var secondComplaint = await jira.CreateComplaintAsync(complaint);
        var correction = await jira.CreateCorrectionAsync(complaint);

        Assert.Equal("COMPLAINT-1001", firstComplaint.IssueKey);
        Assert.Equal("COMPLAINT-1002", secondComplaint.IssueKey);
        Assert.Equal("CORRECTION-2001", correction.IssueKey);
    }

    [Fact]
    public async Task BlobStorageClientReturnsFakeBlobUrls()
    {
        var storage = new FakeBlobStorageClient();
        var attachments = new[]
        {
            new IncomingAttachmentDto("att-001", "defect photo.jpg", "image/jpeg", 2048)
        };

        var stored = await storage.StoreAttachmentsAsync(new ComplaintId("CMP-2026-0001"), attachments);
        var attachment = Assert.Single(stored);

        Assert.Equal("att-001", attachment.AttachmentId);
        Assert.Equal("https://mock.blob.local/complaints/CMP-2026-0001/att-001/defect%20photo.jpg", attachment.StorageUri);
    }

    [Fact]
    public async Task SapClientSimulatesSpecialFailureOrders()
    {
        var sap = new FakeSapClient();

        await Assert.ThrowsAsync<TimeoutException>(() => sap.GetOrderAsync("SAP-TIMEOUT"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sap.GetOrderAsync("SAP-RATE-LIMIT"));

        Assert.Equal("Simulated SAP rate limit.", exception.Message);
    }
}
