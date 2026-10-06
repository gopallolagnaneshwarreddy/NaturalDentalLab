namespace DentalLab.Application.Security;

public static class ApplicationRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Staff = "Staff";

    public static readonly string[] All =
    [
        Owner,
        Admin,
        Staff
    ];
}
