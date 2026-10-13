namespace ClaimDesk.Api.Domain;

public enum Role
{
    Agent,
    Adjuster,
    Manager,
}

public enum PolicyType
{
    Motor,
    Health,
    Home,
}

public enum PolicyStatus
{
    Active,
    Cancelled,
    Expired,
}

public enum ClaimStatus
{
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    Paid,
}
