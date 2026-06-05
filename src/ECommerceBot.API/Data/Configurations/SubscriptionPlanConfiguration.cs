using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Tier).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(p => p.YearlyPrice).HasPrecision(18, 2);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        builder.HasMany(p => p.Tenants)
               .WithOne(t => t.Plan)
               .HasForeignKey(t => t.PlanId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
