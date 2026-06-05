using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class LicenseInfoConfiguration : IEntityTypeConfiguration<LicenseInfo>
{
    public void Configure(EntityTypeBuilder<LicenseInfo> b)
    {
        b.HasKey(l => l.Id);

        b.Property(l => l.TenantId).IsRequired();
        b.HasIndex(l => l.LicenseKey).IsUnique();
        b.HasIndex(l => l.IsActive);
        b.HasIndex(l => l.ExpiresAt);

        b.Property(l => l.LicenseKey).IsRequired().HasMaxLength(512);
        b.Property(l => l.OwnerName).IsRequired().HasMaxLength(200);
        b.Property(l => l.OwnerEmail).HasMaxLength(256);
        b.Property(l => l.CustomerName).HasMaxLength(200);
        b.Property(l => l.ProductName).IsRequired().HasMaxLength(100);
        b.Property(l => l.Edition).IsRequired().HasMaxLength(50);
        b.Property(l => l.BotUsername).HasMaxLength(100);
        b.Property(l => l.AllowedDomain).HasMaxLength(256);
        b.Property(l => l.ServerFingerprint).HasMaxLength(128);
        b.Property(l => l.Signature).IsRequired().HasMaxLength(2048);
        b.Property(l => l.Notes).HasMaxLength(1000);
    }
}
