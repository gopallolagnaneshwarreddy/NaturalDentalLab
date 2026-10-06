namespace DentalLab.Web;

/// <summary>
/// When this copy of the app was built, shown in the public home-page footer
/// so it is easy to check which version is live on the website.
/// </summary>
public static class BuildInfo
{
    public static string Stamp { get; } = Create();

    private static string Create()
    {
        try
        {
            var path = typeof(BuildInfo).Assembly.Location;
            var built = File.GetLastWriteTimeUtc(path);
            var india = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
            return "build " + TimeZoneInfo.ConvertTimeFromUtc(built, india).ToString("yyyy.MM.dd-HHmm");
        }
        catch
        {
            return "build unknown";
        }
    }
}
