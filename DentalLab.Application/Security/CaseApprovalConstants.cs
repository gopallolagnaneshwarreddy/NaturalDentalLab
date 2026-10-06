namespace DentalLab.Application.Security;

public static class CaseApprovalRequestTypes
{
    public const string Edit = "Edit";
    public const string Cancel = "Cancel";
    public static readonly string[] All = [Edit, Cancel];
}

public static class CaseApprovalRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Completed = "Completed";
    public const string Withdrawn = "Withdrawn";
}
