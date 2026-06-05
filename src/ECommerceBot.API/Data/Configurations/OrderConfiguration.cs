using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.TotalAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.ReceiptPhotoFileId).HasMaxLength(200);
        builder.Property(o => o.ReceiptPhotoUniqueId).HasMaxLength(200);
        builder.Property(o => o.AccountDetails).HasMaxLength(500);
        builder.Property(o => o.AdminNotes).HasMaxLength(1000);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        builder.HasIndex(o => o.ReceiptPhotoUniqueId)
               .IsUnique()
               .HasFilter("[ReceiptPhotoUniqueId] IS NOT NULL");

        builder.HasMany(o => o.OrderItems)
               .WithOne(oi => oi.Order)
               .HasForeignKey(oi => oi.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Transaction)
               .WithOne(t => t.Order)
               .HasForeignKey<Transaction>(t => t.OrderId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
