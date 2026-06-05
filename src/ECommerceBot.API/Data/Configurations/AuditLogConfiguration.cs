using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.Action).IsRequired().HasMaxLength(100);
        builder.Property(a => a.TargetType).HasMaxLength(50);
        builder.Property(a => a.Details).HasMaxLength(1000);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasIndex(a => a.AdminId);
        builder.HasIndex(a => a.CreatedAt);

        builder.HasOne(a => a.Admin)
               .WithMany()
               .HasForeignKey(a => a.AdminId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
