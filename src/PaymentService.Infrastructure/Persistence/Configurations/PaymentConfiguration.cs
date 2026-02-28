using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Enums.Payments;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.OrderId)
            .HasColumnName("order_id");

        builder.Property(p => p.UserId)
            .HasColumnName("user_id");

        // Status stored as string for readability in the database
        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        // Money value object mapped as owned entity (two columns: amount + currency)
        builder.OwnsOne(p => p.Money, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        // Index on order_id FK for general queries
        builder.HasIndex(p => p.OrderId, "ix_payments_order_id");
        builder.HasIndex(p => p.UserId);

        // Partial unique index: only one Successful payment is allowed per order.
        // EF Core partial indexes are defined via HasFilter.
        builder.HasIndex(p => p.OrderId, "ix_payments_order_id_successful")
            .HasFilter("status = 'Successful'")
            .IsUnique();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Order is configured in OrderConfiguration.
    }
}
