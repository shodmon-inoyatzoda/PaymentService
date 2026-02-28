using FluentAssertions;
using PaymentService.Application.Features.Payments.Services;
using PaymentService.Infrastructure.Payments;

namespace PaymentService.Application.Tests.Payments;

/// <summary>
/// Unit tests for FakePaymentProviderClient and ResilientPaymentProviderClient.
/// </summary>
public class FakePaymentProviderClientTests
{
    private static ProviderChargeRequest MakeRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD");

    // ─────────────────────── FakePaymentProviderClient ───

    [Fact]
    public async Task FakeProvider_WhenSuccessRateIs1_AlwaysSucceeds()
    {
        var provider = new FakePaymentProviderClient(new FakeProviderOptions { SuccessRate = 1.0 });
        var request = MakeRequest();

        var result = await provider.ChargeAsync(request, CancellationToken.None);

        result.Status.Should().Be(ProviderChargeStatus.Succeeded);
        result.ProviderReferenceId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FakeProvider_WhenAlwaysUnavailable_ThrowsTransientException()
    {
        var provider = new FakePaymentProviderClient(new FakeProviderOptions { AlwaysUnavailable = true });
        var request = MakeRequest();

        var act = async () => await provider.ChargeAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<ProviderTransientException>();
    }

    [Fact]
    public async Task FakeProvider_WhenAlwaysDecline_ReturnsFailed()
    {
        var provider = new FakePaymentProviderClient(new FakeProviderOptions { AlwaysDecline = true });
        var request = MakeRequest();

        var result = await provider.ChargeAsync(request, CancellationToken.None);

        result.Status.Should().Be(ProviderChargeStatus.Failed);
        result.ProviderReferenceId.Should().BeNull();
    }

    [Fact]
    public async Task FakeProvider_IsDeterministicForSamePaymentId()
    {
        var options = new FakeProviderOptions { SuccessRate = 0.5 };
        var provider = new FakePaymentProviderClient(options);
        var request = MakeRequest(); // fixed PaymentId + OrderId

        // Same request should always produce the same outcome
        ProviderChargeStatus? firstOutcome = null;
        for (var i = 0; i < 5; i++)
        {
            ProviderChargeStatus outcome;
            try
            {
                var result = await provider.ChargeAsync(request, CancellationToken.None);
                outcome = result.Status;
            }
            catch (ProviderTransientException)
            {
                outcome = ProviderChargeStatus.Unavailable;
            }

            if (firstOutcome is null)
                firstOutcome = outcome;
            else
                outcome.Should().Be(firstOutcome, "outcome must be deterministic for the same PaymentId");
        }
    }

    // ─────────────────────── ResilientPaymentProviderClient ───

    [Fact]
    public async Task ResilientProvider_WhenInnerSucceeds_ReturnsSuccess()
    {
        var inner = new FakePaymentProviderClient(new FakeProviderOptions { SuccessRate = 1.0 });
        var resilient = new ResilientPaymentProviderClient(inner);
        var request = MakeRequest();

        var result = await resilient.ChargeAsync(request, CancellationToken.None);

        result.Status.Should().Be(ProviderChargeStatus.Succeeded);
    }

    [Fact]
    public async Task ResilientProvider_WhenInnerDeclines_ReturnsFailed()
    {
        var inner = new FakePaymentProviderClient(new FakeProviderOptions { AlwaysDecline = true });
        var resilient = new ResilientPaymentProviderClient(inner);
        var request = MakeRequest();

        var result = await resilient.ChargeAsync(request, CancellationToken.None);

        result.Status.Should().Be(ProviderChargeStatus.Failed);
    }

    [Fact]
    public async Task ResilientProvider_WhenInnerAlwaysUnavailable_ReturnsUnavailableQuickly()
    {
        // Polly will retry twice then give up; the wrapper maps the exception to Unavailable.
        var inner = new FakePaymentProviderClient(new FakeProviderOptions { AlwaysUnavailable = true });
        var resilient = new ResilientPaymentProviderClient(inner);
        var request = MakeRequest();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await resilient.ChargeAsync(request, CancellationToken.None);
        stopwatch.Stop();

        result.Status.Should().Be(ProviderChargeStatus.Unavailable);
        // Polly timeout is 2 s per attempt + 2 retries; total wall-clock should still be well under 10 s
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ResilientProvider_CircuitOpens_AfterRepeatedFailures()
    {
        // Drive enough failures to trip the circuit breaker (MinimumThroughput = 3, FailureRatio = 0.5)
        var inner = new FakePaymentProviderClient(new FakeProviderOptions { AlwaysUnavailable = true });
        var resilient = new ResilientPaymentProviderClient(inner);
        var request = MakeRequest();

        // Make several calls to trip the circuit
        for (var i = 0; i < 6; i++)
        {
            var r = await resilient.ChargeAsync(request, CancellationToken.None);
            r.Status.Should().Be(ProviderChargeStatus.Unavailable);
        }

        // Once the circuit is open the response should come back immediately as Unavailable
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var openResult = await resilient.ChargeAsync(request, CancellationToken.None);
        stopwatch.Stop();

        openResult.Status.Should().Be(ProviderChargeStatus.Unavailable);
        // Circuit-open path should be very fast (no retries / no sleep)
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }
}
