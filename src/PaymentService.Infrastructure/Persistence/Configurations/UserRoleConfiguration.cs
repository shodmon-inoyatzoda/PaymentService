using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Entities.Users;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");

        // Composite primary key
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.UserId)
            .HasColumnName("user_id");

        builder.Property(ur => ur.RoleId)
            .HasColumnName("role_id");

        builder.Property(ur => ur.AssignedAt)
            .HasColumnName("assigned_at");

        // Shadow property for optional AssignedBy FK
        builder.Property<Guid?>("AssignedById")
            .HasColumnName("assigned_by_id");

        builder.HasOne(ur => ur.AssignedBy)
            .WithMany()
            .HasForeignKey("AssignedById")
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // FK on user_id and role_id; relationships are configured in User/Role configurations.
        builder.HasIndex(ur => ur.UserId);
        builder.HasIndex(ur => ur.RoleId);
        builder.HasIndex("AssignedById");
    }
}
