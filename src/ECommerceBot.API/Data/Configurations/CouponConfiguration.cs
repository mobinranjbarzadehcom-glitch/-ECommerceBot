using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.DiscountValue).HasColumnType("decimal(18,2)");
        builder.Property(c => c.MinOrderAmount).HasColumnType("decimal(18,2)");
        builder.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
    }
}
