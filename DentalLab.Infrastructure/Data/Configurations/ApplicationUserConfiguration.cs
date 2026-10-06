using DentalLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DentalLab.Infrastructure.Data.Configurations;

public sealed class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(
        EntityTypeBuilder<ApplicationUser> entity)
    {
        entity.Property(
                user => user.MustChangePassword)
            .HasDefaultValue(false)
            .IsRequired();

        entity.Property(
                user => user.InvitationEmailStatus)
            .HasMaxLength(20)
            .HasDefaultValue("NotSent")
            .IsRequired();

        entity.Property(
                user => user.InvitationEmailError)
            .HasMaxLength(2000);
    }
}