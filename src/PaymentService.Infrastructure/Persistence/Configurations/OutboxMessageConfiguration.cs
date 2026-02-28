using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id");

        builder.Property(o => o.OccurredOn)
            .HasColumnName("occurred_on")
            .IsRequired();

        builder.Property(o => o.Type)
            .HasColumnName("type")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(o => o.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(o => o.ProcessedOn)
            .HasColumnName("processed_on");

        builder.Property(o => o.Error)
            .HasColumnName("error")
            .HasColumnType("text");

        builder.Property(o => o.RetryCount)
            .HasColumnName("retry_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(o => o.ProcessedOn)
            .HasDatabaseName("ix_outbox_messages_processed_on");

        builder.HasIndex(o => o.OccurredOn)
            .HasDatabaseName("ix_outbox_messages_occurred_on");
    }
}
