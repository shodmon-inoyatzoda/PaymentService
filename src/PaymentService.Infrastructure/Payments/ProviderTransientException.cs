namespace PaymentService.Infrastructure.Payments;

/// <summary>
/// Thrown by provider implementations to indicate a transient failure (network error,
/// service temporarily unavailable, etc.) that Polly retry/circuit-breaker should handle.
/// </summary>
public sealed class ProviderTransientException : Exception
{
    public ProviderTransientException(string message) : base(message) { }
    public ProviderTransientException(string message, Exception inner) : base(message, inner) { }
}
