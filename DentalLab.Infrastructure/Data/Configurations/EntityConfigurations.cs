using DentalLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DentalLab.Infrastructure.Data.Configurations;

public sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> e) { e.ToTable("Doctors", "dbo", t => t.ExcludeFromMigrations()); e.HasKey(x => x.DocId); e.Property(x => x.DocName).HasMaxLength(100).IsRequired(); e.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false); e.Property(x => x.Email).HasMaxLength(150); e.Property(x => x.CreatedBy).HasMaxLength(256); e.Property(x => x.ModifiedBy).HasMaxLength(256); }
}
public sealed class CaseTypeConfiguration : IEntityTypeConfiguration<CaseType>
{
    public void Configure(EntityTypeBuilder<CaseType> e) { e.ToTable("CaseTypes", "dbo", t => t.ExcludeFromMigrations()); e.HasKey(x => x.CaseTypeId); e.HasAlternateKey(x => new { x.CaseTypeId, x.Scope }); e.Property(x => x.CaseTypeCode).HasMaxLength(20).IsUnicode(false).IsRequired(); e.Property(x => x.CaseTypeName).HasMaxLength(100).IsRequired(); e.Property(x => x.Scope).HasColumnType("char(1)"); e.Property(x => x.CreatedBy).HasMaxLength(256); e.Property(x => x.ModifiedBy).HasMaxLength(256); }
}
public sealed class DoctorRateConfiguration : IEntityTypeConfiguration<DoctorRate>
{
    public void Configure(EntityTypeBuilder<DoctorRate> e) { e.ToTable("DoctorRates", "dbo", t => t.ExcludeFromMigrations()); e.HasKey(x => new { x.DocId, x.CaseTypeId }); e.Property(x => x.Cost).HasColumnType("decimal(10,2)"); e.Property(x => x.CreatedBy).HasMaxLength(256); e.Property(x => x.ModifiedBy).HasMaxLength(256); e.HasOne(x => x.Doctor).WithMany(x => x.Rates).HasForeignKey(x => x.DocId).OnDelete(DeleteBehavior.Restrict); e.HasOne(x => x.CaseType).WithMany(x => x.DoctorRates).HasForeignKey(x => x.CaseTypeId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class RecordConfiguration : IEntityTypeConfiguration<Record>
{
    public void Configure(EntityTypeBuilder<Record> e) { e.ToTable("Records", "dbo", t => t.ExcludeFromMigrations()); e.HasKey(x => new { x.CaseId, x.LineNumber }); e.Property(x => x.CaseId).ValueGeneratedNever(); e.Property(x => x.Scope).HasColumnType("char(1)"); e.Property(x => x.Arch).HasColumnType("char(1)"); e.Property(x => x.UR).HasMaxLength(8).IsUnicode(false); e.Property(x => x.UL).HasMaxLength(8).IsUnicode(false); e.Property(x => x.LR).HasMaxLength(8).IsUnicode(false); e.Property(x => x.LL).HasMaxLength(8).IsUnicode(false); e.Property(x => x.PatientName).HasMaxLength(100); e.Property(x => x.PatientRef).HasMaxLength(50); e.Property(x => x.UnitRate).HasColumnType("decimal(10,2)"); e.Property(x => x.Amount).HasColumnType("decimal(12,2)"); e.Property(x => x.Notes).HasMaxLength(250); e.Property(x => x.CancelReason).HasMaxLength(250); e.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired(); e.Property(x => x.ModifiedBy).HasMaxLength(256); e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken(); e.Property(x => x.Units).ValueGeneratedOnAddOrUpdate().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore); e.Property(x => x.Amount).ValueGeneratedOnAddOrUpdate().Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore); e.HasOne(x => x.Doctor).WithMany(x => x.Records).HasForeignKey(x => x.DocId).OnDelete(DeleteBehavior.Restrict); e.HasOne(x => x.CaseType).WithMany(x => x.Records).HasForeignKey(x => new { x.CaseTypeId, x.Scope }).HasPrincipalKey(x => new { x.CaseTypeId, x.Scope }).OnDelete(DeleteBehavior.Restrict); e.HasOne(x => x.DoctorRate).WithMany().HasForeignKey(x => new { x.DocId, x.CaseTypeId }).OnDelete(DeleteBehavior.Restrict); }
}