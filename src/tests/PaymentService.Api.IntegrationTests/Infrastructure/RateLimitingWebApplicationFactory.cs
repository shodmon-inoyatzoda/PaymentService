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
/// Isolated factory used exclusively by rate-limiting tests.
/// It configures very low rate limits so tests can verify the 429 behaviour
/// without interfering with the shared "Integration" collection.
/// </summary>
public sealed class RateLimitingWebApplicationFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
#pragma warning disable CS0618
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
#pragma warning restore CS0618
        .WithImage("postgres:17-alpine")
        .WithDatabase("ratelimit_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FakePaymentProvider:SuccessRate"] = "1.0",
                ["FakePaymentProvider:DelayMs"]     = "0",

                // Low limits so tests can trigger 429 quickly
                ["RateLimiting:Global:PermitLimit"]        = "100",
                ["RateLimiting:Global:WindowSeconds"]      = "60",
                ["RateLimiting:Auth:PermitLimit"]          = "2",
                ["RateLimiting:Auth:WindowSeconds"]        = "60",
                ["RateLimiting:PaymentConfirm:PermitLimit"]   = "2",
                ["RateLimiting:PaymentConfirm:WindowSeconds"] = "60",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var dbCtxOptions = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbCtxOptions is not null)
                services.Remove(dbCtxOptions);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

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

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        return host;
    }
}
