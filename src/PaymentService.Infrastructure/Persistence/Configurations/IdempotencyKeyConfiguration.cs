using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Application.Idempotency;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(i => i.Key)
            .HasColumnName("key")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(i => i.ResponseStatusCode)
            .HasColumnName("response_status_code")
            .IsRequired();

        builder.Property(i => i.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(i => i.ExpiresAt)
            .HasColumnName("expires_at");

        builder.HasIndex(i => new { i.UserId, i.Key })
            .IsUnique()
            .HasDatabaseName("ix_idempotency_keys_user_id_key");
    }
}
