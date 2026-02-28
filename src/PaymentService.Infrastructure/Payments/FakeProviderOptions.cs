namespace PaymentService.Infrastructure.Payments;

public sealed class FakeProviderOptions
{
    public const string SectionName = "FakePaymentProvider";

    /// <summary>
    /// When true the provider always throws <see cref="ProviderTransientException"/> (simulates down provider).
    /// Takes precedence over <see cref="AlwaysDecline"/> and <see cref="SuccessRate"/>.
    /// </summary>
    public bool AlwaysUnavailable { get; set; } = false;

    /// <summary>
    /// When true the provider always returns <see cref="PaymentService.Application.Features.Payments.Services.ProviderChargeStatus.Failed"/>
    /// (simulates a card decline — not retryable).
    /// Takes precedence over <see cref="SuccessRate"/>.
    /// </summary>
    public bool AlwaysDecline { get; set; } = false;

    /// <summary>
    /// Fraction of calls that succeed (0.0–1.0). Remaining calls throw
    /// <see cref="ProviderTransientException"/>. Default 1.0 (always succeed).
    /// Outcome is deterministic — based on a hash of the PaymentId.
    /// </summary>
    public double SuccessRate { get; set; } = 1.0;

    /// <summary>Artificial delay added before responding, in milliseconds. 0 = no delay.</summary>
    public int DelayMs { get; set; } = 0;
}
