namespace PaymentService.Application.Common;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(string eventType, string eventContent, CancellationToken cancellationToken = default);
}
