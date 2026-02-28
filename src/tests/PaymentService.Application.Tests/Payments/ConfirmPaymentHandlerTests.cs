using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features.Auth.Commands.Register;
using PaymentService.Application.Features.Payments.Commands.ConfirmPayment;
using PaymentService.Application.Features.Payments.Commands.CreatePayment;
using PaymentService.Application.Features.Payments.Services;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Orders;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Enums.Orders;
using PaymentService.Domain.Enums.Payments;
using PaymentService.Infrastructure.Persistence;
using NSubstitute;

namespace PaymentService.Application.Tests.Payments;

/// <summary>
/// CQRS handler tests for ConfirmPayment using SQLite in-memory.
/// The IOrderLockService is a no-op in these tests; the SELECT FOR UPDATE behaviour
/// is exercised against a real PostgreSQL database in integration tests.
/// </summary>
public class ConfirmPaymentHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _rtg;
    private readonly IOrderLockService _noOpLockService;

    public ConfirmPaymentHandlerTests()
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

    private async Task<Guid> RegisterUserAsync(string phone = "+998901234567", string email = "test@example.com")
    {
        var handler = new RegisterCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RegisterCommand(phone, email, "Test User", "Password1", "127.0.0.1"),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        return result.Value.UserId;
    }

    private async Task<Order> CreateOrderAsync(Guid userId, decimal amount = 100m, string currency = "USD")
    {
        var ctx = CreateContext();
        var orderResult = Order.Create(userId, amount, currency);
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

    private ConfirmPaymentCommandHandler CreateHandler() =>
        new(CreateContext(), _noOpLockService);

    // ─────────────────────────────── ConfirmPayment ───

    [Fact]
    public async Task ConfirmPayment_WithValidPendingPayment_Succeeds()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);
        var paymentId = await CreatePaymentAsync(userId, order.Id);

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(PaymentStatus.Successful);
        result.Value.Id.Should().Be(paymentId);

        // Verify order was marked as Paid
        var ctx = CreateContext();
        var dbOrder = await ctx.Orders.FindAsync(order.Id);
        dbOrder!.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task ConfirmPayment_ForNonExistentPayment_ReturnsNotFound()
    {
        var userId = await RegisterUserAsync();

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ConfirmPayment_ForAnotherUsersPayment_ReturnsNotFound()
    {
        var userId = await RegisterUserAsync();
        var otherUserId = await RegisterUserAsync("+998907654321", "other@example.com");
        var order = await CreateOrderAsync(otherUserId);
        var paymentId = await CreatePaymentAsync(otherUserId, order.Id);

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ConfirmPayment_WhenOrderAlreadyPaid_ReturnsConflict()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);
        var paymentId = await CreatePaymentAsync(userId, order.Id);

        // Simulate order already paid (e.g. by another request that completed first)
        var ctx = CreateContext();
        var dbOrder = await ctx.Orders.FindAsync(order.Id);
        dbOrder!.MarkAsPaid();
        await ctx.SaveChangesAsync();

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task ConfirmPayment_WhenPaymentAlreadyCompleted_ReturnsConflict()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);
        var paymentId = await CreatePaymentAsync(userId, order.Id);

        // Confirm once to mark payment as Successful
        var firstResult = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);
        firstResult.IsSuccess.Should().BeTrue();

        // Try to confirm the same payment again
        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, paymentId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task ConfirmPayment_WhenAnotherPaymentAlreadySuccessful_ReturnsConflict()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);

        // Create two payments for the same order
        var payment1Id = await CreatePaymentAsync(userId, order.Id);
        var payment2Id = await CreatePaymentAsync(userId, order.Id);

        // Confirm payment1 successfully
        var firstConfirm = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, payment1Id),
            CancellationToken.None);
        firstConfirm.IsSuccess.Should().BeTrue();

        // Try to confirm payment2 — order is now Paid, so it should fail
        var secondConfirm = await CreateHandler().Handle(
            new ConfirmPaymentCommand(userId, payment2Id),
            CancellationToken.None);

        secondConfirm.IsFailure.Should().BeTrue();
        secondConfirm.Error.Type.Should().Be(ErrorType.Conflict);

        // Verify only one successful payment exists
        var ctx = CreateContext();
        var successfulPayments = await ctx.Payments
            .Where(p => p.OrderId == order.Id && p.Status == PaymentStatus.Successful)
            .ToListAsync();
        successfulPayments.Should().HaveCount(1);
        successfulPayments[0].Id.Should().Be(payment1Id);
    }

    /// <summary>
    /// Simulates two parallel confirm attempts on the same order (sequential execution).
    /// With SELECT FOR UPDATE on PostgreSQL, one would block and see the committed state.
    /// Here we verify the invariant check prevents a double-confirm even without real locking.
    /// Only one attempt succeeds; the other returns Conflict.
    /// </summary>
    [Fact]
    public async Task ConfirmPayment_TwoAttemptsOnSameOrder_OnlyOneSucceeds()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);

        var payment1Id = await CreatePaymentAsync(userId, order.Id);
        var payment2Id = await CreatePaymentAsync(userId, order.Id);

        // Simulate sequential execution of two "concurrent" confirmation attempts
        var handler1 = CreateHandler();
        var handler2 = CreateHandler();

        var result1 = await handler1.Handle(
            new ConfirmPaymentCommand(userId, payment1Id), CancellationToken.None);

        var result2 = await handler2.Handle(
            new ConfirmPaymentCommand(userId, payment2Id), CancellationToken.None);

        // Exactly one should succeed
        var successes = new[] { result1, result2 }.Count(r => r.IsSuccess);
        var conflicts = new[] { result1, result2 }.Count(r => r.IsFailure && r.Error.Type == ErrorType.Conflict);

        successes.Should().Be(1);
        conflicts.Should().Be(1);

        // Order must be Paid
        var ctx = CreateContext();
        var dbOrder = await ctx.Orders.FindAsync(order.Id);
        dbOrder!.Status.Should().Be(OrderStatus.Paid);

        // Exactly one successful payment
        var successfulCount = await ctx.Payments
            .CountAsync(p => p.OrderId == order.Id && p.Status == PaymentStatus.Successful);
        successfulCount.Should().Be(1);
    }
}
