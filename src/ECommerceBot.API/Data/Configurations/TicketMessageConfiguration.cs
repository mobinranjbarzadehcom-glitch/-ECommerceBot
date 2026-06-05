using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");

        builder.HasKey(tm => tm.Id);

        builder.Property(tm => tm.Content).IsRequired().HasMaxLength(2000);
        builder.Property(tm => tm.CreatedAt).IsRequired();
        builder.Property(tm => tm.UpdatedAt).IsRequired();

        builder.HasOne(tm => tm.Sender)
               .WithMany(u => u.TicketMessages)
               .HasForeignKey(tm => tm.SenderId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
