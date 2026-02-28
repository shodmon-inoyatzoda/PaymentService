using PaymentService.Domain.Common;

namespace PaymentService.Domain.Events;

public sealed record OrderPaidDomainEvent(Guid OrderId) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
