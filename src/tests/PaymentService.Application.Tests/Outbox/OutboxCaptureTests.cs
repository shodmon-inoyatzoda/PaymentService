using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features.Auth.Commands.Register;
using PaymentService.Application.Features.Payments.Commands.ConfirmPayment;
using PaymentService.Application.Features.Payments.Commands.CreatePayment;
using PaymentService.Application.Features.Payments.Services;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Domain.Entities.Orders;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Events;
using PaymentService.Infrastructure.Payments;
using PaymentService.Infrastructure.Persistence;
using NSubstitute;
using System.Text.Json;

namespace PaymentService.Application.Tests.Outbox;

/// <summary>
/// Tests verifying that domain events raised during payment confirmation
/// are captured and persisted as OutboxMessages in the same transaction.
/// </summary>
public class OutboxCaptureTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _rtg;
    private readonly IOrderLockService _noOpLockService;

    public OutboxCaptureTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        _jwt = Substitute.For<IJwtTokenService>();
        _jwt.CreateAccessToken(Arg.Any<User>()).Returns("access-token");

        _rtg = Substitute.For<IRefreshTokenGenerator>();
        _rtg.Generate().Returns(_ => Guid.NewGuid().ToString("N"));

        _noOpLockService = Substitute.For<IOrderLockService>();
        _noOpLockService.AcquireLockAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    public void Dispose() => _connection.Dispose();

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    private async Task<Guid> RegisterUserAsync()
    {
        var handler = new RegisterCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RegisterCommand("+998901234567", "test@example.com", "Test User", "Password1", "127.0.0.1"),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value.UserId;
    }

    private async Task<Order> CreateOrderAsync(Guid userId)
    {
        var ctx = CreateContext();
        var orderResult = Order.Create(userId, 100m, "USD");
        orderResult.IsSuccess.Should().BeTrue();
        var order = orderResult.Value;
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();
        return order;
    }

    private async Task<Guid> CreatePaymentAsync(Guid userId, Guid orderId)
    {
        var handler = new CreatePaymentCommandHandler(CreateContext());
        var result = await handler.Handle(new CreatePaymentCommand(userId, orderId), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value.Id;
    }

    [Fact]
    public async Task ConfirmPayment_WhenOrderPaid_CaptureDomainEventsAsOutboxMessages()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);
        var paymentId = await CreatePaymentAsync(userId, order.Id);

        var provider = new FakePaymentProviderClient(new FakeProviderOptions { SuccessRate = 1.0 });
        var handler = new ConfirmPaymentCommandHandler(CreateContext(), _noOpLockService, provider);

        var result = await handler.Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var ctx = CreateContext();
        var outboxMessages = await ctx.OutboxMessages.ToListAsync();

        // Order.MarkAsPaid raises OrderPaidDomainEvent
        // Payment.MarkAsCompleted raises PaymentSucceededDomainEvent
        outboxMessages.Should().HaveCount(2);

        var types = outboxMessages.Select(m => m.Type).ToList();
        types.Should().Contain(t => t.Contains(nameof(OrderPaidDomainEvent)));
        types.Should().Contain(t => t.Contains(nameof(PaymentSucceededDomainEvent)));
        }

    [Fact]
    public async Task ConfirmPayment_WhenOrderPaid_OutboxMessageContentContainsOrderId()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);
        var paymentId = await CreatePaymentAsync(userId, order.Id);

        var provider = new FakePaymentProviderClient(new FakeProviderOptions { SuccessRate = 1.0 });
        var handler = new ConfirmPaymentCommandHandler(CreateContext(), _noOpLockService, provider);

        var result = await handler.Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var ctx = CreateContext();
        var orderPaidMsg = await ctx.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type.Contains(nameof(OrderPaidDomainEvent)));

        orderPaidMsg.Should().NotBeNull();
        orderPaidMsg!.ProcessedOn.Should().BeNull();
        orderPaidMsg.RetryCount.Should().Be(0);

        var payload = JsonDocument.Parse(orderPaidMsg.Content);
        payload.RootElement.GetProperty("orderId").GetGuid().Should().Be(order.Id);
    }

    [Fact]
    public async Task OutboxMessages_AreNotCreated_WhenPaymentFails()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);
        var paymentId = await CreatePaymentAsync(userId, order.Id);

        var decliningProvider = new FakePaymentProviderClient(new FakeProviderOptions { AlwaysDecline = true });
        var handler = new ConfirmPaymentCommandHandler(CreateContext(), _noOpLockService, decliningProvider);

        var result = await handler.Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();

        var ctx = CreateContext();
        var outboxMessages = await ctx.OutboxMessages.ToListAsync();
        outboxMessages.Should().BeEmpty();
    }
}
