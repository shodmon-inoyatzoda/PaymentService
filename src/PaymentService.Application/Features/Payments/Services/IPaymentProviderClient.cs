namespace PaymentService.Application.Features.Payments.Services;

public interface IPaymentProviderClient
{
    Task<ProviderChargeResult> ChargeAsync(ProviderChargeRequest request, CancellationToken cancellationToken);
}
