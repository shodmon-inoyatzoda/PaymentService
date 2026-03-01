using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaymentService.Application.Common;
using PaymentService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PaymentService.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that boots the API in-memory against a real
/// ephemeral PostgreSQL instance managed by Testcontainers.
/// Shared across all integration test classes via a single
/// <see cref="IntegrationCollection"/> collection fixture.
/// </summary>
public sealed class PaymentServiceWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Suppress the obsolete parameterless-constructor warning: the builder still
    // works correctly and .WithImage() is chained below to pin the image version.
#pragma warning disable CS0618
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
#pragma warning restore CS0618
        .WithImage("postgres:17-alpine")
        .WithDatabase("payment_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    // ── IAsyncLifetime ──────────────────────────────────────────────────────

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    // ── WebApplicationFactory ───────────────────────────────────────────────

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override only runtime-resolved settings (FakePaymentProvider).
        // JWT settings are intentionally left at their appsettings.json defaults
        // so that token generation (IOptions<JwtSettings>) and token validation
        // (TokenValidationParameters set at startup) use the same signing key.
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FakePaymentProvider:SuccessRate"] = "1.0",
                ["FakePaymentProvider:DelayMs"]     = "0",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace the ApplicationDbContext registration so that it uses
            // the ephemeral test container rather than the default connection
            // string captured at startup by AddInfrastructure().
            var dbCtxOptions = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbCtxOptions is not null)
                services.Remove(dbCtxOptions);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Remove all background services (e.g. OutboxProcessor) to avoid
            // background database activity between tests.
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var descriptor in hostedServices)
                services.Remove(descriptor);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Apply EF Core migrations once the host is built and the container
        // is already running (InitializeAsync ran before CreateClient()).
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        return host;
    }
}

