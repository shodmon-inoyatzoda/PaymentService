using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Features.Auth.Commands.Login;
using PaymentService.Application.Features.Auth.Commands.RefreshToken;
using PaymentService.Application.Features.Auth.Commands.Register;
using PaymentService.Application.Features.Auth.Commands.RevokeToken;
using PaymentService.Application.Features.Auth.Queries.GetCurrentUser;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Users;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Application.Tests.Auth;

/// <summary>
/// CQRS handler tests using SQLite in-memory with a fresh DbContext per handler call
/// (matching the scoped-per-request lifetime used in the real application).
/// </summary>
public class AuthHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _rtg;

    public AuthHandlerTests()
    {
        // Keep the connection open so the in-memory database survives across contexts
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Create the schema once
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        _jwt = Substitute.For<IJwtTokenService>();
        _jwt.CreateAccessToken(Arg.Any<User>()).Returns("access-token");

        _rtg = Substitute.For<IRefreshTokenGenerator>();
        // Always return a unique token so unique DB constraints are never violated
        _rtg.Generate().Returns(_ => Guid.NewGuid().ToString("N"));
    }

    public void Dispose() => _connection.Dispose();

    // Creates a fresh DbContext using the shared in-memory connection
    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    // Helper: registers a new user and returns the auth response
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

    // ──────────────────────────────────────────────────────────── Register ──

    [Fact]
    public async Task Register_NewUser_ReturnsAuthResponse()
    {
        var handler = new RegisterCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RegisterCommand("+998901234567", "test@example.com", "Test User", "Password1", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.FullName.Should().Be("Test User");
    }

    [Fact]
    public async Task Register_DuplicatePhone_ReturnsConflict()
    {
        await RegisterUserAsync(phone: "+998901234567", email: "a@example.com");

        var handler = new RegisterCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RegisterCommand("+998901234567", "b@example.com", "Other", "Password1", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.PhoneExists");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        await RegisterUserAsync(phone: "+998901234567", email: "test@example.com");

        var handler = new RegisterCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RegisterCommand("+998907654321", "test@example.com", "Other", "Password1", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.EmailExists");
    }

    // ──────────────────────────────────────────────────────────── Login ──

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAuthResponse()
    {
        await RegisterUserAsync();

        var handler = new LoginCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new LoginCommand("+998901234567", "Password1", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task Login_WithInvalidPhone_ReturnsUnauthorized()
    {
        var handler = new LoginCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new LoginCommand("+998900000000", "Password1", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await RegisterUserAsync();

        var handler = new LoginCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new LoginCommand("+998901234567", "WrongPassword1", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────── Refresh ──

    [Fact]
    public async Task RefreshToken_WithValidToken_RotatesToken()
    {
        var registered = await RegisterUserAsync();
        var originalToken = registered.RefreshToken;

        var handler = new RefreshTokenCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RefreshTokenCommand(originalToken, "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().NotBe(originalToken);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        var handler = new RefreshTokenCommandHandler(CreateContext(), _jwt, _rtg);
        var result = await handler.Handle(
            new RefreshTokenCommand("invalid-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────── Revoke ──

    [Fact]
    public async Task RevokeToken_WithValidToken_Succeeds()
    {
        var registered = await RegisterUserAsync();

        var handler = new RevokeTokenCommandHandler(CreateContext());
        var result = await handler.Handle(
            new RevokeTokenCommand(registered.RefreshToken, "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────── GetCurrentUser ──

    [Fact]
    public async Task GetCurrentUser_WithExistingUser_ReturnsUser()
    {
        var registered = await RegisterUserAsync(fullName: "Test User");

        var handler = new GetCurrentUserQueryHandler(CreateContext());
        var result = await handler.Handle(
            new GetCurrentUserQuery(registered.UserId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Test User");
        result.Value.PhoneNumber.Should().Be("+998901234567");
    }

    [Fact]
    public async Task GetCurrentUser_WithUnknownId_ReturnsNotFound()
    {
        var handler = new GetCurrentUserQueryHandler(CreateContext());
        var result = await handler.Handle(
            new GetCurrentUserQuery(Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
