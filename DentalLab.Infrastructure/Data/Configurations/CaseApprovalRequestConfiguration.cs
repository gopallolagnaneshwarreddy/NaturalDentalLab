using DentalLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalLab.Infrastructure.Data.Configurations;

public sealed class CaseApprovalRequestConfiguration : IEntityTypeConfiguration<CaseApprovalRequest>
{
    public void Configure(EntityTypeBuilder<CaseApprovalRequest> e)
    {
        e.ToTable("CaseApprovalRequests", "dbo", t => t.ExcludeFromMigrations());
        e.HasKey(x => x.RequestId);
        e.Property(x => x.RequestId).ValueGeneratedOnAdd();
        e.Property(x => x.RequestType).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.RequestedByUserId).HasMaxLength(450).IsRequired();
        e.Property(x => x.RequestedByEmail).HasMaxLength(256).IsRequired();
        e.Property(x => x.AssignedAdminUserId).HasMaxLength(450).IsRequired();
        e.Property(x => x.AssignedAdminEmail).HasMaxLength(256).IsRequired();
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.Property(x => x.AdditionalDetails).HasMaxLength(1000);
        e.Property(x => x.ReviewedByUserId).HasMaxLength(450);
        e.Property(x => x.ReviewedByEmail).HasMaxLength(256);
        e.Property(x => x.ReviewComment).HasMaxLength(500);
        e.Property(x => x.CompletedByUserId).HasMaxLength(450);
        e.Property(x => x.CompletedByEmail).HasMaxLength(256);
        e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        e.HasIndex(x => new { x.AssignedAdminUserId, x.Status });
        e.HasIndex(x => new { x.RequestedByUserId, x.Status });
        e.HasIndex(x => x.CaseId);
    }
}
