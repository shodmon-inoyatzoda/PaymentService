using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Common;
using PaymentService.Application.Idempotency;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Orders;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Entities.Users;

namespace PaymentService.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Order
    public DbSet<Order> Orders => Set<Order>();

    // Payment
    public DbSet<Payment> Payments => Set<Payment>();

    // Idempotency
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    // Outbox
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertDomainEventsToOutboxMessages();
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    private void ConvertDomainEventsToOutboxMessages()
    {
        var domainEvents = ChangeTracker
            .Entries<BaseEntity>()
            .Select(e => e.Entity)
            .SelectMany(entity =>
            {
                var events = entity.DomainEvents.ToList();
                entity.ClearDomainEvents();
                return events;
            })
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                OccurredOn = domainEvent.OccurredOn,
                Type = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName!,
                Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
            };
            OutboxMessages.Add(outboxMessage);
        }
    }
}
