namespace PaymentService.Application.Features.Payments.Services;

public sealed record ProviderChargeRequest(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency);
