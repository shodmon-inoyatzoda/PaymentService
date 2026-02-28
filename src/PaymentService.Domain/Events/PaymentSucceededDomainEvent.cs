using PaymentService.Domain.Common;

namespace PaymentService.Domain.Events;

public sealed record PaymentSucceededDomainEvent(Guid PaymentId, Guid OrderId) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
