using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Features.Auth.Commands.Register;
using PaymentService.Application.Features.Orders.Commands.CreateOrder;
using PaymentService.Application.Features.Orders.Queries.GetOrderById;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Enums.Orders;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Application.Tests.Orders;

public class OrderHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _rtg;

    public OrderHandlerTests()
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

    private async Task<AuthResponse> RegisterUserAsync(
        string phone = "+998901234567",
        string email = "test@example.com",
        string fullName = "Test User")
    {
        var handler = new RegisterCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RegisterCommand(phone, email, fullName, "Password1", "127.0.0.1"),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue("setup registration must succeed");
        return result.Value;
    }

    // ──────────────────────────────────────────── CreateOrder ──

    [Fact]
    public async Task CreateOrder_SetsStatusCreatedAndUserId()
    {
        var user = await RegisterUserAsync();

        var handler = new CreateOrderCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreateOrderCommand(user.UserId, 100m, "USD"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(user.UserId);
        result.Value.Status.Should().Be(OrderStatus.Created);
        result.Value.Amount.Should().Be(100m);
        result.Value.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task CreateOrder_WithInvalidAmount_ReturnsValidationError()
    {
        var user = await RegisterUserAsync();

        var handler = new CreateOrderCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreateOrderCommand(user.UserId, -10m, "USD"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidCurrency_ReturnsValidationError()
    {
        var user = await RegisterUserAsync();

        var handler = new CreateOrderCommandHandler(CreateContext());
        var result = await handler.Handle(
            new CreateOrderCommand(user.UserId, 100m, "INVALID"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    // ──────────────────────────────────────────── GetOrderById ──

    [Fact]
    public async Task GetOrderById_OwnOrder_ReturnsOrder()
    {
        var user = await RegisterUserAsync();

        var createHandler = new CreateOrderCommandHandler(CreateContext());
        var created = await createHandler.Handle(
            new CreateOrderCommand(user.UserId, 50m, "EUR"),
            CancellationToken.None);
        created.IsSuccess.Should().BeTrue();

        var queryHandler = new GetOrderByIdQueryHandler(CreateContext());
        var result = await queryHandler.Handle(
            new GetOrderByIdQuery(created.Value.Id, user.UserId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(created.Value.Id);
        result.Value.UserId.Should().Be(user.UserId);
    }

    [Fact]
    public async Task GetOrderById_OtherUsersOrder_ReturnsNotFound()
    {
        var user1 = await RegisterUserAsync(phone: "+998901111111", email: "user1@example.com");
        var user2 = await RegisterUserAsync(phone: "+998902222222", email: "user2@example.com");

        var createHandler = new CreateOrderCommandHandler(CreateContext());
        var created = await createHandler.Handle(
            new CreateOrderCommand(user1.UserId, 50m, "EUR"),
            CancellationToken.None);
        created.IsSuccess.Should().BeTrue();

        var queryHandler = new GetOrderByIdQueryHandler(CreateContext());
        var result = await queryHandler.Handle(
            new GetOrderByIdQuery(created.Value.Id, user2.UserId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetOrderById_NonExistentOrder_ReturnsNotFound()
    {
        var user = await RegisterUserAsync();

        var handler = new GetOrderByIdQueryHandler(CreateContext());
        var result = await handler.Handle(
            new GetOrderByIdQuery(Guid.NewGuid(), user.UserId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
