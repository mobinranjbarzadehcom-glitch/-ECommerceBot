using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class AffiliateReferralConfiguration : IEntityTypeConfiguration<AffiliateReferral>
{
    public void Configure(EntityTypeBuilder<AffiliateReferral> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.BonusAmount).HasColumnType("decimal(18,2)");
        builder.HasIndex(r => new { r.TenantId, r.ReferredUserId }).IsUnique();
        builder.HasOne(r => r.Affiliate)
            .WithMany(a => a.Referrals)
            .HasForeignKey(r => r.AffiliateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ReferredUser)
            .WithMany()
            .HasForeignKey(r => r.ReferredUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
