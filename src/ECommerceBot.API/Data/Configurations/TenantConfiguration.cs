using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.TenantSlug).IsUnique();

        builder.Property(t => t.TenantName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.TenantSlug).IsRequired().HasMaxLength(100);
        builder.Property(t => t.CustomerName).HasMaxLength(200);
        builder.Property(t => t.CustomerPhone).HasMaxLength(30);
        builder.Property(t => t.CustomerEmail).HasMaxLength(256);
        builder.Property(t => t.BotUsername).HasMaxLength(100);
        builder.Property(t => t.BotTokenEncrypted).HasMaxLength(1000);
        builder.Property(t => t.WebhookSecret).HasMaxLength(256);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.SuspendedReason).HasMaxLength(500);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.HasMany(t => t.Notes)
            .WithOne(n => n.Tenant)
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
