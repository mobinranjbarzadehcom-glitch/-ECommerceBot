using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Subject).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();

        builder.HasOne(t => t.User)
               .WithMany(u => u.CreatedTickets)
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedAdmin)
               .WithMany(u => u.AssignedTickets)
               .HasForeignKey(t => t.AssignedAdminId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.RelatedOrder)
               .WithMany(o => o.Tickets)
               .HasForeignKey(t => t.RelatedOrderId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.Messages)
               .WithOne(m => m.Ticket)
               .HasForeignKey(m => m.TicketId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
