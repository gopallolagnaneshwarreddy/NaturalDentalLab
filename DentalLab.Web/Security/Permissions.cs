namespace DentalLab.Web.Security;

/// <summary>
/// Policy names used in [Authorize(Policy = Perm.X)] and in code checks.
/// Each one maps to a permission key ("Cases.Create" etc.) in the catalogue below.
/// </summary>
public static class Perm
{
    public const string Prefix = "Perm:";

    public const string DashboardView = Prefix + "Dashboard.View";

    public const string CasesView = Prefix + "Cases.View";
    public const string CasesCreate = Prefix + "Cases.Create";
    public const string CasesBulkEntry = Prefix + "Cases.BulkEntry";
    public const string CasesViewPrices = Prefix + "Cases.ViewPrices";
    public const string CasesEdit = Prefix + "Cases.Edit";
    public const string CasesCancel = Prefix + "Cases.Cancel";
    public const string CasesRequestChange = Prefix + "Cases.RequestChange";

    public const string ApprovalsView = Prefix + "Approvals.View";

    public const string DoctorsManage = Prefix + "Doctors.Manage";
    public const string CaseTypesManage = Prefix + "CaseTypes.Manage";
    public const string RatesManage = Prefix + "Rates.Manage";
    public const string QuickAddDoctor = Prefix + "QuickAdd.Doctor";
    public const string QuickAddRate = Prefix + "QuickAdd.CaseTypeRate";

    public const string BillingView = Prefix + "Billing.View";
    public const string ReportsMonthly = Prefix + "Reports.Monthly";

    public const string PaymentsView = Prefix + "Payments.View";
    public const string PaymentsRecord = Prefix + "Payments.Record";
    public const string PaymentsVoid = Prefix + "Payments.Void";
    public const string PaymentsSetup = Prefix + "Payments.Setup";

    public const string UsersManage = Prefix + "Users.Manage";

    /// <summary>"Perm:Cases.Create" -> "Cases.Create"</summary>
    public static string KeyOf(string policyOrKey) =>
        policyOrKey.StartsWith(Prefix, StringComparison.Ordinal) ? policyOrKey[Prefix.Length..] : policyOrKey;
}

/// <summary>One switch on the Access Control page.</summary>
public sealed record PermissionDefinition(
    string Policy,
    string Group,
    string Name,
    string Description,
    bool DefaultAdmin,
    bool DefaultStaff)
{
    public string Key => Perm.KeyOf(Policy);
}

/// <summary>
/// The full list of permissions that can be switched for Admin and Staff.
/// Defaults reproduce the app's original (hard-coded) behaviour.
/// The Owner always has everything and is not listed here.
/// </summary>
public static class PermissionCatalog
{
    public const string AdminRole = "Admin";
    public const string StaffRole = "Staff";
    public const string OwnerRole = "Owner";

    /// <summary>Roles whose permissions can be changed on the Access Control page.</summary>
    public static readonly string[] ConfigurableRoles = [AdminRole, StaffRole];

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(Perm.DashboardView, "Overview", "View dashboard", "Open the dashboard page.", true, true),

        new(Perm.CasesView, "Cases", "View cases", "Open the case list and case details.", true, true),
        new(Perm.CasesCreate, "Cases", "Create new case", "Use the New Case page.", true, true),
        new(Perm.CasesBulkEntry, "Cases", "Bulk case entry", "Use Cases by Date and Cases by Doctor.", true, true),
        new(Perm.CasesViewPrices, "Cases", "See prices", "See rates, amounts and totals on case pages.", true, false),
        new(Perm.CasesEdit, "Cases", "Edit cases directly", "Edit a case without an approval request.", true, false),
        new(Perm.CasesCancel, "Cases", "Cancel cases directly", "Cancel a case without an approval request.", true, false),
        new(Perm.CasesRequestChange, "Cases", "Request edit / cancellation", "Send an edit or cancel request for approval.", false, true),

        new(Perm.ApprovalsView, "Approvals", "Approval requests", "Open the approval requests pages.", true, true),

        new(Perm.DoctorsManage, "Master data", "Manage doctors", "Add and edit doctors on the Doctors page.", true, false),
        new(Perm.CaseTypesManage, "Master data", "Manage case types", "Add and edit case types.", true, false),
        new(Perm.RatesManage, "Master data", "Manage doctor rates", "Open Doctor Rates and change rates.", true, false),
        new(Perm.QuickAddDoctor, "Master data", "Quick-add doctor", "\"+ Add new doctor\" from the doctor search box.", false, false),
        new(Perm.QuickAddRate, "Master data", "Quick-add case type / rate", "\"+ Add rate\" and \"+ New case type\" while entering a case.", false, false),

        new(Perm.BillingView, "Billing", "Doctor bill", "Generate, print and download doctor bills.", true, false),
        new(Perm.ReportsMonthly, "Billing", "Monthly summary", "Open, print and download the monthly summary report.", true, false),

        new(Perm.PaymentsView, "Payments", "View dues & payments", "Open Doctor Payments: pending dues, statements and payment history.", true, false),
        new(Perm.PaymentsRecord, "Payments", "Record payments", "Record full or part payments received from doctors.", true, false),
        new(Perm.PaymentsVoid, "Payments", "Void payments", "Cancel a wrongly entered payment (it stays in the history, marked void).", false, false),
        new(Perm.PaymentsSetup, "Payments", "Opening balances", "Set the tracking start date and each doctor's opening balance.", false, false),

        new(Perm.UsersManage, "Administration", "Manage users", "Open the Users page.", true, false),
    ];

    public static PermissionDefinition? Find(string policyOrKey)
    {
        var key = Perm.KeyOf(policyOrKey);
        return All.FirstOrDefault(p => p.Key == key);
    }

    public static bool DefaultFor(string role, PermissionDefinition definition) => role switch
    {
        AdminRole => definition.DefaultAdmin,
        StaffRole => definition.DefaultStaff,
        _ => false
    };
}
