using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class AffiliateConfiguration : IEntityTypeConfiguration<Affiliate>
{
    public void Configure(EntityTypeBuilder<Affiliate> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ReferralCode).IsRequired().HasMaxLength(20);
        builder.Property(a => a.TotalEarnings).HasColumnType("decimal(18,2)");
        builder.HasIndex(a => new { a.TenantId, a.ReferralCode }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.UserId }).IsUnique();
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
