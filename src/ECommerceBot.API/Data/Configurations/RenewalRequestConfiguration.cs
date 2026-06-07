using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class RenewalRequestConfiguration : IEntityTypeConfiguration<RenewalRequest>
{
    public void Configure(EntityTypeBuilder<RenewalRequest> builder)
    {
        builder.ToTable("RenewalRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RequestType).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.PriceAmount).HasColumnType("decimal(18,2)");
        builder.Property(r => r.ReceiptFileId).HasMaxLength(200);
        builder.Property(r => r.ReviewNote).HasMaxLength(500);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.Tenant)
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.NewPlan)
            .WithMany()
            .HasForeignKey(r => r.NewPlanId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
