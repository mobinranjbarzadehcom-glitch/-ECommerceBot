using ECommerceBot.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerceBot.API.Data.Configurations;

public class TenantNoteConfiguration : IEntityTypeConfiguration<TenantNote>
{
    public void Configure(EntityTypeBuilder<TenantNote> builder)
    {
        builder.ToTable("TenantNotes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Note).IsRequired().HasMaxLength(2000);
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.UpdatedAt).IsRequired();
        builder.HasIndex(n => n.TenantId);
    }
}
