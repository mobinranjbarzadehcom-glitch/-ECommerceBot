using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class CouponUsageConfiguration : IEntityTypeConfiguration<CouponUsage>
{
    public void Configure(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasOne(u => u.Coupon)
            .WithMany(c => c.Usages)
            .HasForeignKey(u => u.CouponId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(u => u.Order)
            .WithMany()
            .HasForeignKey(u => u.OrderId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
