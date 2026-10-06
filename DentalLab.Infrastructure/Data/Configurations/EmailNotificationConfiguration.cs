using DentalLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DentalLab.Infrastructure.Data.Configurations;
public sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> e)
    {
        e.ToTable("EmailNotifications", "dbo");
        e.HasKey(x => x.EmailNotificationId);
        e.Property(x => x.RecipientEmail).HasMaxLength(256).IsRequired();
        e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
        e.Property(x => x.NotificationType).HasMaxLength(50).IsRequired();
        e.Property(x => x.RelatedEntityType).HasMaxLength(50);
        e.Property(x => x.RelatedEntityId).HasMaxLength(100);
        e.Property(x => x.Status).HasMaxLength(20).IsRequired();
        e.Property(x => x.ErrorMessage).HasMaxLength(2000);
        e.HasIndex(x => new { x.Status, x.CreatedOn });
    }
}
