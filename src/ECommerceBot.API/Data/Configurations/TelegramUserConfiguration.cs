using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class TelegramUserConfiguration : IEntityTypeConfiguration<TelegramUser>
{
    public void Configure(EntityTypeBuilder<TelegramUser> builder)
    {
        builder.ToTable("TelegramUsers");

        builder.HasKey(u => u.Id);

        builder.HasIndex(u => u.TelegramId).IsUnique();

        builder.Property(u => u.TelegramId).IsRequired();
        builder.Property(u => u.ChatId);
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.Username).HasMaxLength(50);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.WalletBalance).HasPrecision(18, 2);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.CurrentState).HasConversion<string>().HasMaxLength(50);
        builder.Property(u => u.TempData).HasMaxLength(2000);
        builder.Property(u => u.LastActivity);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();

        builder.HasOne(u => u.Cart)
               .WithOne(c => c.User)
               .HasForeignKey<Cart>(c => c.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Orders)
               .WithOne(o => o.User)
               .HasForeignKey(o => o.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Transactions)
               .WithOne(t => t.User)
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
