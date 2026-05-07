namespace Metalpol.Complaints.Domain.Enums;

public enum ComplaintStatus
{
    Received,
    IntakeQueued,
    Parsed,
    MissingData,
    CustomerMatched,
    SapVerificationPending,
    SapVerified,
    SapMismatch,
    JiraComplaintCreated,
    ResponseDrafted,
    HumanReviewRequired,
    CustomerResponseApproved,
    CorrectionCreated,
    Closed,
    Failed,
    DuplicateLinked
}
