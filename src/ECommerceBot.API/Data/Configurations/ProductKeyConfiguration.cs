using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class ProductKeyConfiguration : IEntityTypeConfiguration<ProductKey>
{
    public void Configure(EntityTypeBuilder<ProductKey> builder)
    {
        builder.ToTable("ProductKeys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.KeyValue).IsRequired().HasMaxLength(500);
        builder.Property(k => k.CreatedAt).IsRequired();
        builder.Property(k => k.UpdatedAt).IsRequired();

        builder.HasOne(k => k.OrderItem)
               .WithMany(oi => oi.ProductKeys)
               .HasForeignKey(k => k.OrderItemId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
