using Microsoft.Extensions.Logging;
using PaymentService.Application.Common;

namespace PaymentService.Infrastructure.Outbox;

internal sealed class LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    : IIntegrationEventPublisher
{
    public Task PublishAsync(string eventType, string eventContent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Publishing integration event {EventType}: {EventContent}",
            eventType,
            eventContent);

        return Task.CompletedTask;
    }
}
