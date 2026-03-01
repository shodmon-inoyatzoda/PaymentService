using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PaymentService.Api.IntegrationTests.Helpers;
using PaymentService.Api.IntegrationTests.Infrastructure;
using PaymentService.Application.Auth.DTOs;

namespace PaymentService.Api.IntegrationTests.Auth;

[Collection("Integration")]
public sealed class AuthEndpointsTests
{
    private readonly PaymentServiceWebApplicationFactory _factory;

    public AuthEndpointsTests(PaymentServiceWebApplicationFactory factory) =>
        _factory = factory;

    // ── Register → Login → Me ──────────────────────────────────────────────

    [Fact]
    public async Task Register_Returns201_WithAccessAndRefreshTokens()
    {
        var client = _factory.CreateClient();

        var auth = await AuthHelper.RegisterAsync(
            client, "+15550000001", "reg1@test.com", "Alice");

        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.FullName.Should().Be("Alice");
    }

    [Fact]
    public async Task Login_Returns200_WithTokens()
    {
        var client = _factory.CreateClient();
        await AuthHelper.RegisterAsync(client, "+15550000002", "login1@test.com");

        var auth = await AuthHelper.LoginAsync(client, "+15550000002");

        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_Login_Me_Returns_CurrentUser()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(
            client, "+15550000003", "me1@test.com", "Bob");

        AuthHelper.SetBearerToken(client, auth.AccessToken);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!;
        user.FullName.Should().Be("Bob");
        user.PhoneNumber.Should().Be("+15550000003");
    }

    [Fact]
    public async Task Me_Without_Token_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Refresh ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_Returns_NewAccessToken_That_Authenticates()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(
            client, "+15550000004", "refresh1@test.com");

        var refreshRequest = new RefreshTokenRequest(auth.RefreshToken);
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuth = (await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        newAuth.AccessToken.Should().NotBeNullOrWhiteSpace();
        newAuth.AccessToken.Should().NotBe(auth.AccessToken);

        // The new access token must work
        var newClient = _factory.CreateClient();
        AuthHelper.SetBearerToken(newClient, newAuth.AccessToken);
        var meResponse = await newClient.GetAsync("/api/auth/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Revoke (logout) ────────────────────────────────────────────────────

    [Fact]
    public async Task Revoke_Makes_RefreshToken_Invalid()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(
            client, "+15550000005", "revoke1@test.com");

        // Revoke requires an authenticated request
        AuthHelper.SetBearerToken(client, auth.AccessToken);
        var revokeRequest = new RevokeTokenRequest(auth.RefreshToken);
        var revokeResponse = await client.PostAsJsonAsync("/api/auth/revoke", revokeRequest);
        revokeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Attempting to refresh with the revoked token must fail (400 Validation: token not active)
        var refreshRequest = new RefreshTokenRequest(auth.RefreshToken);
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Duplicate registration ─────────────────────────────────────────────

    [Fact]
    public async Task Register_DuplicatePhone_Returns409()
    {
        var client = _factory.CreateClient();
        await AuthHelper.RegisterAsync(client, "+15550000006", "dup1@test.com");

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("+15550000006", "dup2@test.com", "Copy", "TestPassword123!"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
