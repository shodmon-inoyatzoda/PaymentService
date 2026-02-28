namespace PaymentService.Application.Features.Payments.Services;

public enum ProviderChargeStatus
{
    Succeeded,
    Failed,
    Unavailable,
    Timeout
}

public sealed record ProviderChargeResult(
    ProviderChargeStatus Status,
    string? ProviderReferenceId,
    string? ErrorMessage);
