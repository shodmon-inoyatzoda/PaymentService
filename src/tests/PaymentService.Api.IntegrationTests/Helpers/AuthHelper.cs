using System.Net.Http.Headers;
using System.Net.Http.Json;
using PaymentService.Application.Auth.DTOs;

namespace PaymentService.Api.IntegrationTests.Helpers;

/// <summary>
/// Helper methods shared across integration test classes for auth flows,
/// bearer-token setup and idempotency headers.
/// </summary>
public static class AuthHelper
{
    /// <summary>Registers a new user and returns the auth response.</summary>
    public static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string phoneNumber,
        string email,
        string fullName = "Test User",
        string password = "TestPassword123!")
    {
        var request = new RegisterRequest(phoneNumber, email, fullName, password);
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>Logs in with the given credentials and returns the auth response.</summary>
    public static async Task<AuthResponse> LoginAsync(
        HttpClient client,
        string phoneNumber,
        string password = "TestPassword123!")
    {
        var request = new LoginRequest(phoneNumber, password);
        var response = await client.PostAsJsonAsync("/api/auth/login", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>Registers, then logs in and returns the auth response.</summary>
    public static async Task<AuthResponse> RegisterAndLoginAsync(
        HttpClient client,
        string phoneNumber,
        string email,
        string fullName = "Test User",
        string password = "TestPassword123!")
    {
        await RegisterAsync(client, phoneNumber, email, fullName, password);
        return await LoginAsync(client, phoneNumber, password);
    }

    /// <summary>Sets the Authorization header on the client to the supplied Bearer token.</summary>
    public static void SetBearerToken(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

    /// <summary>
    /// Builds an <see cref="HttpRequestMessage"/> with an <c>Idempotency-Key</c> header.
    /// No request body is set; callers should set <see cref="HttpRequestMessage.Content"/>
    /// when required.
    /// </summary>
    public static HttpRequestMessage BuildRequestWithIdempotencyKey(
        HttpMethod method,
        string url,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return request;
    }
}
