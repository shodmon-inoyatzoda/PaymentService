using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using PaymentService.Application.Auth;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Users;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Application.Tests.Auth;

/// <summary>
/// AuthService tests using SQLite in-memory with a fresh DbContext per service call
/// (matching the scoped-per-request lifetime used in the real application).
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenGenerator _rtg;

    public AuthServiceTests()
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

    // Creates a fresh AuthService with its own short-lived DbContext (like scoped DI)
    private AuthService CreateService()
        => new AuthService(CreateContext(), _jwt, _rtg);

    // Helper: registers a new user and returns the auth response
    private async Task<AuthResponse> RegisterUserAsync(
        string phone = "+998901234567",
        string email = "test@example.com",
        string fullName = "Test User")
    {
        var svc = CreateService();
        var result = await svc.RegisterAsync(
            new RegisterRequest(phone, email, fullName, "Password1"),
            "127.0.0.1");
        result.IsSuccess.Should().BeTrue("setup registration must succeed");
        return result.Value;
    }

    // ──────────────────────────────────────────────────────────── Register ──

    [Fact]
    public async Task RegisterAsync_NewUser_ReturnsAuthResponse()
    {
        var result = await CreateService().RegisterAsync(
            new RegisterRequest("+998901234567", "test@example.com", "Test User", "Password1"),
            "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.FullName.Should().Be("Test User");
    }

    [Fact]
    public async Task RegisterAsync_DuplicatePhone_ReturnsConflict()
    {
        await RegisterUserAsync(phone: "+998901234567", email: "a@example.com");

        var result = await CreateService().RegisterAsync(
            new RegisterRequest("+998901234567", "b@example.com", "Other", "Password1"),
            "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.PhoneExists");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsConflict()
    {
        await RegisterUserAsync(phone: "+998901234567", email: "test@example.com");

        var result = await CreateService().RegisterAsync(
            new RegisterRequest("+998907654321", "test@example.com", "Other", "Password1"),
            "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.EmailExists");
    }

    // ──────────────────────────────────────────────────────────── Login ──

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
    {
        await RegisterUserAsync();

        var result = await CreateService().LoginAsync(
            new LoginRequest("+998901234567", "Password1"),
            "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPhone_ReturnsUnauthorized()
    {
        var result = await CreateService().LoginAsync(
            new LoginRequest("+998900000000", "Password1"),
            "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsUnauthorized()
    {
        await RegisterUserAsync();

        var result = await CreateService().LoginAsync(
            new LoginRequest("+998901234567", "WrongPassword1"),
            "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────── Refresh ──

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_RotatesToken()
    {
        var registered = await RegisterUserAsync();
        var originalToken = registered.RefreshToken;

        var result = await CreateService().RefreshTokenAsync(
            new RefreshTokenRequest(originalToken),
            "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().NotBe(originalToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithInvalidToken_ReturnsUnauthorized()
    {
        var result = await CreateService().RefreshTokenAsync(
            new RefreshTokenRequest("invalid-token"),
            "127.0.0.1");

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────── Revoke ──

    [Fact]
    public async Task RevokeTokenAsync_WithValidToken_Succeeds()
    {
        var registered = await RegisterUserAsync();

        var result = await CreateService().RevokeTokenAsync(
            new RevokeTokenRequest(registered.RefreshToken),
            "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────── GetCurrentUser ──

    [Fact]
    public async Task GetCurrentUserAsync_WithExistingUser_ReturnsUser()
    {
        var registered = await RegisterUserAsync(fullName: "Test User");

        var result = await CreateService().GetCurrentUserAsync(registered.UserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.FullName.Should().Be("Test User");
        result.Value.PhoneNumber.Should().Be("+998901234567");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithUnknownId_ReturnsNotFound()
    {
        var result = await CreateService().GetCurrentUserAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
