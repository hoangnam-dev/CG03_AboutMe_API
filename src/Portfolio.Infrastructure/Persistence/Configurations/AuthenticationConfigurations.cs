using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Portfolio.Application.Common.Models;
using Portfolio.Infrastructure.Authentication;

namespace Portfolio.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.AuthVersion)
            .HasColumnName("AuthVersion")
            .HasDefaultValue(0);
        builder.Property(user => user.IsDisabled)
            .HasColumnName("IsDisabled")
            .HasDefaultValue(false);
    }
}

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("auth_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasColumnName("id");
        builder.Property(session => session.UserId).HasColumnName("user_id");
        builder.Property(session => session.CreatedAt).HasColumnName("created_at");
        builder.Property(session => session.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(session => session.IdleExpiresAt).HasColumnName("idle_expires_at");
        builder.Property(session => session.AbsoluteExpiresAt).HasColumnName("absolute_expires_at");
        builder.Property(session => session.RevokedAt).HasColumnName("revoked_at");
        builder.Property(session => session.RevokeReason).HasColumnName("revoke_reason").HasMaxLength(100);
        builder.Property(session => session.CreatedIp).HasColumnName("created_ip").HasMaxLength(64).IsRequired();
        builder.Property(session => session.LastIp).HasColumnName("last_ip").HasMaxLength(64).IsRequired();
        builder.Property(session => session.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(session => session.RefreshTokens)
            .WithOne(token => token.Session)
            .HasForeignKey(token => token.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(session => new { session.UserId, session.RevokedAt })
            .HasDatabaseName("ix_auth_sessions_user_active");
        builder.HasIndex(session => session.AbsoluteExpiresAt)
            .HasDatabaseName("ix_auth_sessions_absolute_expiry");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_auth_sessions_expiry_order",
                "idle_expires_at <= absolute_expires_at");
        });
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id");
        builder.Property(token => token.SessionId).HasColumnName("session_id");
        builder.Property(token => token.SecretHash).HasColumnName("secret_hash").HasMaxLength(32).IsRequired();
        builder.Property(token => token.ParentTokenId).HasColumnName("parent_token_id");
        builder.Property(token => token.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
        builder.Property(token => token.CreatedAt).HasColumnName("created_at");
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.Property(token => token.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(token => token.RevokedAt).HasColumnName("revoked_at");
        builder.Property(token => token.RevokeReason).HasColumnName("revoke_reason").HasMaxLength(100);
        builder.Property(token => token.CreatedByIp).HasColumnName("created_by_ip").HasMaxLength(64).IsRequired();
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(token => token.ParentTokenId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(token => token.SessionId)
            .IsUnique()
            .HasFilter("consumed_at IS NULL AND revoked_at IS NULL")
            .HasDatabaseName("ux_refresh_tokens_one_active_per_session");
        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expiry");
        builder.HasIndex(token => token.ParentTokenId)
            .HasDatabaseName("ix_refresh_tokens_parent");
        builder.HasIndex(token => token.ReplacedByTokenId)
            .HasDatabaseName("ix_refresh_tokens_replacement");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_refresh_tokens_hash_length",
                "octet_length(secret_hash) = 32");
        });
    }
}
