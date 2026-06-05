using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class PaymentCardConfiguration : IEntityTypeConfiguration<PaymentCard>
{
    public void Configure(EntityTypeBuilder<PaymentCard> builder)
    {
        builder.ToTable("PaymentCards");

        builder.HasKey(pc => pc.Id);

        builder.Property(pc => pc.CardNumber).IsRequired().HasMaxLength(30);
        builder.Property(pc => pc.CardHolderName).IsRequired().HasMaxLength(100);
        builder.Property(pc => pc.BankName).IsRequired().HasMaxLength(100);
        builder.Property(pc => pc.CreatedAt).IsRequired();
        builder.Property(pc => pc.UpdatedAt).IsRequired();
    }
}
