using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;

namespace Portfolio.Infrastructure.Persistence.Configurations;

internal sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable(
            "contact_messages",
            table => table.HasCheckConstraint("ck_contact_messages_status", "status IN ('new', 'read', 'archived')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.SenderName).HasColumnName("sender_name").HasMaxLength(150).IsRequired();
        builder.Property(x => x.SenderEmail).HasColumnName("sender_email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasColumnName("message").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<ContactStatus>(value, true)).HasDefaultValue(ContactStatus.New);
        builder.Property(x => x.IsSpam).HasColumnName("is_spam").HasDefaultValue(false);
        builder.Property(x => x.IpHash).HasColumnName("ip_hash").HasMaxLength(128);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.ReadAt).HasColumnName("read_at");
        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("idx_contact_messages_admin_filter")
            .IsDescending(false, true);
    }
}

internal sealed class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.ToTable("site_settings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(100);
        builder.Property(x => x.Value).HasColumnName("value").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
    }
}
