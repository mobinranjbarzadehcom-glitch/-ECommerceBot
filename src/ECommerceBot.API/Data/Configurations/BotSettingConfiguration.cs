using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class BotSettingConfiguration : IEntityTypeConfiguration<BotSetting>
{
    public void Configure(EntityTypeBuilder<BotSetting> builder)
    {
        builder.ToTable("BotSettings");

        builder.HasKey(bs => bs.Id);

        builder.Property(bs => bs.Key).IsRequired().HasMaxLength(100);
        builder.Property(bs => bs.Value).IsRequired().HasMaxLength(1000);
        builder.Property(bs => bs.Description).HasMaxLength(500);
        builder.Property(bs => bs.CreatedAt).IsRequired();
        builder.Property(bs => bs.UpdatedAt).IsRequired();

        builder.HasIndex(bs => bs.Key).IsUnique();
    }
}
