namespace PaymentService.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}