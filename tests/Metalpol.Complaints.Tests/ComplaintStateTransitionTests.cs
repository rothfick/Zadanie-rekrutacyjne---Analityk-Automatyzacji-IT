using Metalpol.Complaints.Domain;
using Metalpol.Complaints.Domain.Entities;
using Metalpol.Complaints.Domain.Enums;
using Metalpol.Complaints.Domain.Events;
using Metalpol.Complaints.Domain.ValueObjects;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class ComplaintStateTransitionTests
{
    [Fact]
    public void ReceiveEmailCreatesComplaintInReceivedState()
    {
        var receivedAt = new DateTimeOffset(2026, 5, 7, 10, 0, 0, TimeSpan.Zero);

        var complaint = Complaint.ReceiveEmail(
            new ComplaintId("CMP-2026-0001"),
            "message-001",
            receivedAt);

        Assert.Equal(ComplaintStatus.Received, complaint.Status);
        Assert.Equal("message-001", complaint.MessageId);
        Assert.Equal(receivedAt, complaint.ReceivedAt);
        Assert.IsType<EmailReceived>(Assert.Single(complaint.DomainEvents));
    }

    [Fact]
    public void HappyPathTransitionsToClosedWithCorrection()
    {
        var complaint = CreateParsedComplaint();

        complaint.MatchCustomer("CUST-1001");
        complaint.VerifyOrder(SapVerificationResult.Verified("MP-2026-1042"));
        complaint.VerifyBatch(SapVerificationResult.Verified("MP-2026-1042", "B-2026-07-19", "LINE-2"));
        complaint.CreateJiraComplaint(new JiraIssueRef("REK-1001", "Complaint"));
        complaint.DraftResponse("Draft response for customer review.");
        complaint.ApproveCustomerResponse();
        complaint.CreateCorrectionTicket(new JiraIssueRef("REK-1002", "Correction"));
        complaint.Close();

        Assert.Equal(ComplaintStatus.Closed, complaint.Status);
        Assert.Equal("REK-1001", complaint.JiraComplaint?.IssueKey);
        Assert.Equal("REK-1002", complaint.CorrectionTicket?.IssueKey);
        Assert.Contains(complaint.DomainEvents, e => e is EmailReceived);
        Assert.Contains(complaint.DomainEvents, e => e is ComplaintParsed);
        Assert.Contains(complaint.DomainEvents, e => e is DefectClassified);
        Assert.Contains(complaint.DomainEvents, e => e is CustomerMatched);
        Assert.Contains(complaint.DomainEvents, e => e is OrderVerified);
        Assert.Contains(complaint.DomainEvents, e => e is BatchVerified);
        Assert.Contains(complaint.DomainEvents, e => e is JiraComplaintCreated);
        Assert.Contains(complaint.DomainEvents, e => e is ResponseDrafted);
        Assert.Contains(complaint.DomainEvents, e => e is CorrectionTicketCreated);
        Assert.Contains(complaint.DomainEvents, e => e is ComplaintClosed);
    }

    [Fact]
    public void ParseWithMissingFieldsTransitionsToMissingData()
    {
        var complaint = Complaint.ReceiveEmail(new ComplaintId("CMP-2026-0002"), "message-002");
        complaint.QueueIntake();

        complaint.Parse(new AiTriageResult(
            "pl",
            orderNumber: null,
            description: "Brak numeru zamówienia.",
            DefectCategory.Unknown,
            0.55m,
            new[] { "orderNumber" }));

        Assert.Equal(ComplaintStatus.MissingData, complaint.Status);
        Assert.Equal(DefectCategory.Unknown, complaint.DefectCategory);
    }

    [Fact]
    public void RequestHumanReviewTransitionsFromParsed()
    {
        var complaint = CreateParsedComplaint();

        complaint.RequestHumanReview("Low confidence classification.");

        Assert.Equal(ComplaintStatus.HumanReviewRequired, complaint.Status);
        Assert.Equal("Low confidence classification.", complaint.HumanReviewReason);
        Assert.Contains(complaint.DomainEvents, e => e is HumanReviewRequested);
    }

    [Fact]
    public void LinkDuplicateClosesThroughDuplicateLinkedState()
    {
        var complaint = CreateParsedComplaint();

        complaint.LinkDuplicate(new ComplaintId("CMP-2026-0000"));
        complaint.Close();

        Assert.Equal(ComplaintStatus.Closed, complaint.Status);
        Assert.Equal(new ComplaintId("CMP-2026-0000"), complaint.LinkedDuplicateComplaintId);
        Assert.Contains(complaint.DomainEvents, e => e is DuplicateLinked);
    }

    [Fact]
    public void CreatingJiraComplaintBeforeSapVerificationIsRejected()
    {
        var complaint = CreateParsedComplaint();

        var exception = Assert.Throws<DomainException>(
            () => complaint.CreateJiraComplaint(new JiraIssueRef("REK-1001", "Complaint")));

        Assert.Equal("Jira Complaint can only be created after SAP verification.", exception.Message);
        Assert.Equal(ComplaintStatus.Parsed, complaint.Status);
    }

    [Fact]
    public void BatchVerificationBeforeOrderVerificationIsRejected()
    {
        var complaint = CreateParsedComplaint();
        complaint.MatchCustomer("CUST-1001");

        var exception = Assert.Throws<DomainException>(
            () => complaint.VerifyBatch(SapVerificationResult.Verified("MP-2026-1042", "B-2026-07-19")));

        Assert.Equal("Order must be verified before batch verification.", exception.Message);
        Assert.Equal(ComplaintStatus.CustomerMatched, complaint.Status);
    }

    [Fact]
    public void ClosingParsedComplaintIsRejected()
    {
        var complaint = CreateParsedComplaint();

        var exception = Assert.Throws<DomainException>(() => complaint.Close());

        Assert.Equal("Complaint can only be closed after approval, correction creation or duplicate link.", exception.Message);
        Assert.Equal(ComplaintStatus.Parsed, complaint.Status);
    }

    [Fact]
    public void TerminalComplaintCannotRequestHumanReview()
    {
        var complaint = CreateParsedComplaint();
        complaint.Fail("Unrecoverable integration failure.");

        var exception = Assert.Throws<DomainException>(
            () => complaint.RequestHumanReview("Late review request."));

        Assert.Equal("Human review cannot be requested for a terminal complaint.", exception.Message);
        Assert.Equal(ComplaintStatus.Failed, complaint.Status);
    }

    private static Complaint CreateParsedComplaint()
    {
        var complaint = Complaint.ReceiveEmail(new ComplaintId("CMP-2026-0001"), "message-001");

        complaint.QueueIntake();
        complaint.StoreAttachments(new[]
        {
            new AttachmentInfo("att-001", "defect-photo.jpg", "image/jpeg", 1_024, "mock://blob/att-001")
        });
        complaint.Parse(new AiTriageResult(
            "pl",
            "MP-2026-1042",
            "Rysy i przebarwienia na elemencie.",
            DefectCategory.Visual,
            0.91m));

        return complaint;
    }
}
