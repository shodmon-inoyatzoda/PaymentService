using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentService.Application.Common;
using PaymentService.Infrastructure.Outbox;
using PaymentService.Infrastructure.Persistence;
using NSubstitute;

namespace PaymentService.Application.Tests.Outbox;

/// <summary>
/// Tests verifying that OutboxProcessor fetches unprocessed messages and marks them as processed.
/// </summary>
public class OutboxProcessorTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public OutboxProcessorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    private IServiceScopeFactory BuildScopeFactory(IIntegrationEventPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateContext());
        services.AddScoped(_ => publisher);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>
    /// Seeds an OutboxMessage directly. OutboxMessage is not a BaseEntity so our
    /// SaveChangesAsync override will not attempt to harvest domain events from it.
    /// </summary>
    private async Task<Guid> SeedOutboxMessageAsync(DateTimeOffset? processedOn = null)
    {
        var ctx = CreateContext();
        var msg = new OutboxMessage
        {
            OccurredOn = DateTimeOffset.UtcNow,
            Type = "TestEvent",
            Content = "{}",
            ProcessedOn = processedOn,
        };
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();
        return msg.Id;
    }

    [Fact]
    public async Task ProcessBatch_MarksUnprocessedMessageAsProcessed()
    {
        var msgId = await SeedOutboxMessageAsync();

        var publisher = Substitute.For<IIntegrationEventPublisher>();
        publisher.PublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var processor = new OutboxProcessor(BuildScopeFactory(publisher), NullLogger<OutboxProcessor>.Instance);
        await processor.ProcessBatchAsync(CancellationToken.None);

        await publisher.Received(1).PublishAsync("TestEvent", "{}", Arg.Any<CancellationToken>());

        var reloadCtx = CreateContext();
        var message = await reloadCtx.OutboxMessages.FindAsync(msgId);
        message.Should().NotBeNull();
        message!.ProcessedOn.Should().NotBeNull();
        message.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProcessBatch_AlreadyProcessedMessages_AreSkipped()
    {
        await SeedOutboxMessageAsync(processedOn: DateTimeOffset.UtcNow.AddHours(-1));

        var publisher = Substitute.For<IIntegrationEventPublisher>();

        var processor = new OutboxProcessor(BuildScopeFactory(publisher), NullLogger<OutboxProcessor>.Instance);
        await processor.ProcessBatchAsync(CancellationToken.None);

        await publisher.DidNotReceive().PublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessBatch_WhenPublisherThrows_IncrementsRetryCountAndStoresError()
    {
        var msgId = await SeedOutboxMessageAsync();

        var publisher = Substitute.For<IIntegrationEventPublisher>();
        publisher.PublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("publish failed"));

        var processor = new OutboxProcessor(BuildScopeFactory(publisher), NullLogger<OutboxProcessor>.Instance);
        await processor.ProcessBatchAsync(CancellationToken.None);

        var reloadCtx = CreateContext();
        var message = await reloadCtx.OutboxMessages.FindAsync(msgId);
        message.Should().NotBeNull();
        message!.ProcessedOn.Should().BeNull();
        message.RetryCount.Should().Be(1);
        message.Error.Should().Contain("publish failed");
    }
}
