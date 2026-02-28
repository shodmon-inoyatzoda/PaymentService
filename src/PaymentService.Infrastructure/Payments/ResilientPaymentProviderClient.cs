using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using PaymentService.Application.Features.Payments.Services;

namespace PaymentService.Infrastructure.Payments;

/// <summary>
/// Decorator that wraps an <see cref="IPaymentProviderClient"/> implementation with
/// Polly resilience policies: timeout (2 s), bounded retry (2 attempts), and
/// circuit breaker (opens after 50% failure rate over 3+ calls in a 10 s window).
/// All Polly exceptions are translated to <see cref="ProviderChargeResult"/> values
/// so callers never see raw infrastructure exceptions.
/// </summary>
public sealed class ResilientPaymentProviderClient : IPaymentProviderClient
{
    private readonly IPaymentProviderClient _inner;
    private readonly ResiliencePipeline<ProviderChargeResult> _pipeline;

    public ResilientPaymentProviderClient(IPaymentProviderClient inner)
    {
        _inner = inner;
        _pipeline = BuildPipeline();
    }

    public async Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async ct => await _inner.ChargeAsync(request, ct),
                cancellationToken);
        }
        catch (TimeoutRejectedException)
        {
            return new ProviderChargeResult(ProviderChargeStatus.Timeout, null,
                "Payment provider did not respond in time.");
        }
        catch (BrokenCircuitException)
        {
            return new ProviderChargeResult(ProviderChargeStatus.Unavailable, null,
                "Payment provider circuit is open; too many recent failures.");
        }
        catch (ProviderTransientException ex)
        {
            return new ProviderChargeResult(ProviderChargeStatus.Unavailable, null, ex.Message);
        }
    }

    private static ResiliencePipeline<ProviderChargeResult> BuildPipeline()
    {
        var shouldHandle = new PredicateBuilder<ProviderChargeResult>()
            .Handle<ProviderTransientException>()
            .Handle<TimeoutRejectedException>();

        return new ResiliencePipelineBuilder<ProviderChargeResult>()
            // Timeout: abort any single attempt after 2 seconds
            .AddTimeout(TimeSpan.FromSeconds(2))
            // Retry: up to 2 retries for transient/timeout failures with small constant backoff
            .AddRetry(new RetryStrategyOptions<ProviderChargeResult>
            {
                ShouldHandle = shouldHandle,
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(100)
            })
            // Circuit breaker: open when ≥ 50 % of at least 3 calls in a 10 s window fail;
            // stay open for 30 s before allowing a probe request.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<ProviderChargeResult>
            {
                ShouldHandle = shouldHandle,
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .Build();
    }
}
