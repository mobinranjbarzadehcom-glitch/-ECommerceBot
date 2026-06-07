using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class ScheduledBroadcastConfiguration : IEntityTypeConfiguration<ScheduledBroadcast>
{
    public void Configure(EntityTypeBuilder<ScheduledBroadcast> builder)
    {
        builder.ToTable("ScheduledBroadcasts");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.HtmlMessage).IsRequired().HasMaxLength(4096);
        builder.Property(b => b.TargetFilter).HasConversion<string>().HasMaxLength(30);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();
        builder.HasIndex(b => b.TenantId);
        builder.HasIndex(b => new { b.Status, b.ScheduledAt });

        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
