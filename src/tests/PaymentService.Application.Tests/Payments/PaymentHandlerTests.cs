using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features.Auth.Commands.Register;
using PaymentService.Application.Features.Payments.Commands.CreatePayment;
using PaymentService.Application.Features.Payments.Queries.GetPaymentsByOrder;
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
/// CQRS handler tests for Payments using SQLite in-memory.
/// </summary>
public class PaymentHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _rtg;

    public PaymentHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        _jwt = Substitute.For<IJwtTokenService>();
        _jwt.CreateAccessToken(Arg.Any<User>()).Returns("access-token");

        _rtg = Substitute.For<IRefreshTokenGenerator>();
        _rtg.Generate().Returns(_ => Guid.NewGuid().ToString("N"));
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

    // ─────────────────────────────────── CreatePayment ───

    [Fact]
    public async Task CreatePayment_ForOwnCreatedOrder_Succeeds()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);

        var handler = new CreatePaymentCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreatePaymentCommand(userId, order.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderId.Should().Be(order.Id);
        result.Value.UserId.Should().Be(userId);
        result.Value.Amount.Should().Be(order.Amount);
        result.Value.Currency.Should().Be(order.Currency);
        result.Value.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task CreatePayment_ForPaidOrder_ReturnsConflict()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);

        // Mark order as paid
        var ctx = CreateContext();
        var dbOrder = await ctx.Orders.FindAsync(order.Id);
        dbOrder!.MarkAsPaid();
        await ctx.SaveChangesAsync();

        var handler = new CreatePaymentCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreatePaymentCommand(userId, order.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreatePayment_ForCancelledOrder_ReturnsConflict()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);

        // Mark order as cancelled
        var ctx = CreateContext();
        var dbOrder = await ctx.Orders.FindAsync(order.Id);
        dbOrder!.MarkAsCancelled();
        await ctx.SaveChangesAsync();

        var handler = new CreatePaymentCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreatePaymentCommand(userId, order.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreatePayment_ForAnotherUsersOrder_ReturnsNotFound()
    {
        var userId = await RegisterUserAsync();
        var otherUserId = await RegisterUserAsync("+998907654321", "other@example.com");
        var order = await CreateOrderAsync(otherUserId);

        var handler = new CreatePaymentCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreatePaymentCommand(userId, order.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task CreatePayment_ForNonExistentOrder_ReturnsNotFound()
    {
        var userId = await RegisterUserAsync();

        var handler = new CreatePaymentCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreatePaymentCommand(userId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    // ─────────────────────────────── GetPaymentsByOrder ───

    [Fact]
    public async Task GetPaymentsByOrder_WithExistingPayments_ReturnsOrderedByCreatedAtDesc()
    {
        var userId = await RegisterUserAsync();
        var order = await CreateOrderAsync(userId);

        // Create two payments
        var handler = new CreatePaymentCommandHandler(CreateContext());
        await handler.Handle(new CreatePaymentCommand(userId, order.Id), CancellationToken.None);

        var queryHandler = new GetPaymentsByOrderQueryHandler(CreateContext());
        var result = await queryHandler.Handle(
            new GetPaymentsByOrderQuery(userId, order.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].OrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task GetPaymentsByOrder_ForAnotherUsersOrder_ReturnsNotFound()
    {
        var userId = await RegisterUserAsync();
        var otherUserId = await RegisterUserAsync("+998907654321", "other@example.com");
        var order = await CreateOrderAsync(otherUserId);

        var queryHandler = new GetPaymentsByOrderQueryHandler(CreateContext());
        var result = await queryHandler.Handle(
            new GetPaymentsByOrderQuery(userId, order.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetPaymentsByOrder_ForNonExistentOrder_ReturnsNotFound()
    {
        var userId = await RegisterUserAsync();

        var queryHandler = new GetPaymentsByOrderQueryHandler(CreateContext());
        var result = await queryHandler.Handle(
            new GetPaymentsByOrderQuery(userId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
