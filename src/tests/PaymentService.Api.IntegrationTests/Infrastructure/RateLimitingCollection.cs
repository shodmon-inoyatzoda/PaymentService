using PaymentService.Api.IntegrationTests.Infrastructure;

namespace PaymentService.Api.IntegrationTests;

/// <summary>
/// Defines the xUnit test collection that groups all rate-limiting integration tests.
/// Uses <see cref="RateLimitingWebApplicationFactory"/> which configures very low
/// rate limits to enable threshold testing.
/// </summary>
[CollectionDefinition("RateLimiting")]
public sealed class RateLimitingCollection : ICollectionFixture<RateLimitingWebApplicationFactory> { }
