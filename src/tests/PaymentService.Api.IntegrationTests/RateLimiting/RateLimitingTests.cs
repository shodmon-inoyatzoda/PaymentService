using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PaymentService.Api.IntegrationTests.Helpers;
using PaymentService.Api.IntegrationTests.Infrastructure;
using PaymentService.Application.Auth.DTOs;

namespace PaymentService.Api.IntegrationTests.RateLimiting;

/// <summary>
/// Verifies that rate limiting policies behave correctly.
/// Uses <see cref="RateLimitingWebApplicationFactory"/> which sets very low limits
/// (2 per 60 s for Auth and PaymentConfirm) so the threshold is reachable in tests.
///
/// Each test uses a unique <c>X-Forwarded-For</c> IP (or unique user) to ensure
/// tests that share the same factory instance don't interfere with each other.
/// </summary>
[Collection("RateLimiting")]
public sealed class RateLimitingTests
{
    private readonly RateLimitingWebApplicationFactory _factory;

    public RateLimitingTests(RateLimitingWebApplicationFactory factory) =>
        _factory = factory;

    // ── Auth endpoint rate limiting ────────────────────────────────────────

    /// <summary>
    /// Sending more than PermitLimit (2) requests to an auth endpoint from the
    /// same IP within the window must return 429 on the third request.
    /// </summary>
    [Fact]
    public async Task AuthEndpoint_Returns429_AfterPermitLimitExceeded()
    {
        var client = _factory.CreateClient();
        const string uniqueIp = "10.99.1.1";

        // First two requests reach the controller (may return 400/401 – not 429)
        for (var i = 0; i < 2; i++)
        {
            var req = BuildLoginRequest(uniqueIp);
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: $"request {i + 1} should pass through the rate limiter");
        }

        // Third request must be rejected by the rate limiter
        var limitedReq = BuildLoginRequest(uniqueIp);
        var limitedResp = await client.SendAsync(limitedReq);

        limitedResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// A 429 response must contain a <c>Retry-After</c> header.
    /// </summary>
    [Fact]
    public async Task AuthEndpoint_429Response_IncludesRetryAfterHeader()
    {
        var client = _factory.CreateClient();
        const string uniqueIp = "10.99.1.2";

        for (var i = 0; i < 2; i++)
            await client.SendAsync(BuildLoginRequest(uniqueIp));

        var limitedResp = await client.SendAsync(BuildLoginRequest(uniqueIp));

        limitedResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limitedResp.Headers.Should().ContainKey("Retry-After");
    }

    // ── Per-user rate limiting (authenticated) ─────────────────────────────

    /// <summary>
    /// An authenticated user hitting the Auth policy limit must be keyed by
    /// user ID, not by IP. Two different IPs using the same bearer token share
    /// the same bucket and together exhaust the limit.
    /// </summary>
    [Fact]
    public async Task AuthenticatedUser_IsLimited_ByUserId()
    {
        // Register and log in with a unique user for this test
        var setupClient = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(
            setupClient, "+15580000001", "rl_user1@test.com");

        var client = _factory.CreateClient();
        AuthHelper.SetBearerToken(client, auth.AccessToken);

        // Use two different X-Forwarded-For IPs with the same user token
        const string ipA = "10.99.2.1";
        const string ipB = "10.99.2.2";

        // First request from ipA – should pass (count = 1 for this user)
        var req1 = BuildLoginRequest(ipA);
        req1.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var resp1 = await client.SendAsync(req1);
        resp1.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        // Second request from ipB – should pass (count = 2 for this user)
        var req2 = BuildLoginRequest(ipB);
        req2.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var resp2 = await client.SendAsync(req2);
        resp2.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

        // Third request – limit (2) exhausted, both IPs share the user bucket
        var req3 = BuildLoginRequest(ipA);
        req3.Headers.Authorization = client.DefaultRequestHeaders.Authorization;
        var resp3 = await client.SendAsync(req3);
        resp3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ── Per-IP rate limiting (unauthenticated) ─────────────────────────────

    /// <summary>
    /// Unauthenticated requests must be limited by IP address.
    /// Two different IPs must have independent buckets.
    /// </summary>
    [Fact]
    public async Task UnauthenticatedRequests_AreLimited_ByIp()
    {
        var client = _factory.CreateClient();
        const string ipA = "10.99.3.1";
        const string ipB = "10.99.3.2";

        // Exhaust ipA's auth bucket
        for (var i = 0; i < 2; i++)
            await client.SendAsync(BuildLoginRequest(ipA));

        // ipA is now limited
        var limitedForA = await client.SendAsync(BuildLoginRequest(ipA));
        limitedForA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // ipB still has a fresh bucket and must not be limited
        var notLimitedForB = await client.SendAsync(BuildLoginRequest(ipB));
        notLimitedForB.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static HttpRequestMessage BuildLoginRequest(string forwardedIp)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("+10000000000", "wrong_password"))
        };
        msg.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedIp);
        return msg;
    }
}
