using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Common;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Outbox;

internal class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unexpected error in OutboxProcessor loop.");
            }
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var messages = await FetchUnprocessedMessagesAsync(db, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message.Type, message.Content, cancellationToken);
                message.ProcessedOn = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.ToString();
                logger.LogError(ex, "Failed to process outbox message {MessageId} (retry {RetryCount}).",
                    message.Id, message.RetryCount);
            }
        }

        if (messages.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<List<Persistence.OutboxMessage>> FetchUnprocessedMessagesAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        // Use SELECT ... FOR UPDATE SKIP LOCKED on PostgreSQL to prevent concurrent workers
        // from picking up the same messages. Fall back to a plain EF query for other providers.
        if (db.Database.IsNpgsql())
        {
            return await db.OutboxMessages
                .FromSqlRaw(
                    $"""
                    SELECT * FROM outbox_messages
                    WHERE processed_on IS NULL
                    ORDER BY occurred_on
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);
        }

        // SQLite does not support DateTimeOffset in ORDER BY. Ordering by Id is equivalent
        // because Ids are created via Guid.CreateVersion7() which is time-ordered.
        return await db.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
    }
}
