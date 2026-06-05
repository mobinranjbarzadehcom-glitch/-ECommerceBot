using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransactions");

        builder.HasKey(wt => wt.Id);

        builder.Property(wt => wt.TenantId).IsRequired();
        builder.Property(wt => wt.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(wt => wt.BalanceBefore).IsRequired().HasPrecision(18, 2);
        builder.Property(wt => wt.BalanceAfter).IsRequired().HasPrecision(18, 2);
        builder.Property(wt => wt.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(wt => wt.Description).HasMaxLength(500);
        builder.Property(wt => wt.CreatedAt).IsRequired();
        builder.Property(wt => wt.UpdatedAt).IsRequired();

        builder.HasOne(wt => wt.User)
               .WithMany(u => u.WalletTransactions)
               .HasForeignKey(wt => wt.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(wt => wt.RelatedOrder)
               .WithMany(o => o.WalletTransactions)
               .HasForeignKey(wt => wt.RelatedOrderId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
