using DentalLab.Domain.Entities;
using DentalLab.Infrastructure.Data.Configurations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace DentalLab.Infrastructure.Data;
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<CaseType> CaseTypes => Set<CaseType>();
    public DbSet<DoctorRate> DoctorRates => Set<DoctorRate>();
    public DbSet<Record> Records => Set<Record>();
    public DbSet<CaseApprovalRequest> CaseApprovalRequests => Set<CaseApprovalRequest>();
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();
    protected override void OnModelCreating(ModelBuilder builder) { base.OnModelCreating(builder); builder.ApplyConfigurationsFromAssembly(typeof(DoctorConfiguration).Assembly); }
    public async Task<int> NextCaseIdAsync(CancellationToken ct = default)
    {
        await using var cmd = Database.GetDbConnection().CreateCommand(); cmd.CommandText = "SELECT NEXT VALUE FOR dbo.Seq_CaseId;";
        if (cmd.Connection!.State != System.Data.ConnectionState.Open) await Database.OpenConnectionAsync(ct);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }
}
