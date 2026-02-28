using Microsoft.Extensions.Options;
using PaymentService.Application.Features.Payments.Services;

namespace PaymentService.Infrastructure.Payments;

/// <summary>
/// Deterministic fake payment provider suitable for integration tests.
/// Behaviour is fully configurable via <see cref="FakeProviderOptions"/>.
/// </summary>
public sealed class FakePaymentProviderClient : IPaymentProviderClient
{
    private readonly FakeProviderOptions _options;

    public FakePaymentProviderClient(IOptions<FakeProviderOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>Constructor for use in unit tests where DI is not available.</summary>
    public FakePaymentProviderClient(FakeProviderOptions options)
    {
        _options = options;
    }

    public async Task<ProviderChargeResult> ChargeAsync(
        ProviderChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (_options.DelayMs > 0)
            await Task.Delay(_options.DelayMs, cancellationToken);

        if (_options.AlwaysUnavailable)
            throw new ProviderTransientException(
                $"Fake provider is configured as unavailable for payment '{request.PaymentId}'.");

        if (_options.AlwaysDecline)
            return new ProviderChargeResult(
                ProviderChargeStatus.Failed,
                null,
                $"Fake provider declined payment '{request.PaymentId}'.");

        // Deterministic outcome based on payment-id hash and configured success rate
        if (_options.SuccessRate < 1.0)
        {
            var hash = (uint)HashCode.Combine(request.PaymentId, request.OrderId);
            var fraction = (hash % 100) / 100.0;
            if (fraction >= _options.SuccessRate)
                throw new ProviderTransientException(
                    $"Fake provider simulated transient failure for payment '{request.PaymentId}'.");
        }

        return new ProviderChargeResult(
            ProviderChargeStatus.Succeeded,
            $"FAKE-{request.PaymentId:N}",
            null);
    }
}
