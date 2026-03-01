using PaymentService.Api.IntegrationTests.Infrastructure;

namespace PaymentService.Api.IntegrationTests;

/// <summary>
/// Defines the xUnit test collection that groups all integration test classes.
/// All classes in the "Integration" collection share a single
/// <see cref="PaymentServiceWebApplicationFactory"/> instance (one PostgreSQL
/// container, one migrated database) and run sequentially, which avoids the
/// process-wide Serilog static-logger conflict that arises when multiple
/// WebApplicationFactory instances are created in parallel.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<PaymentServiceWebApplicationFactory> { }
