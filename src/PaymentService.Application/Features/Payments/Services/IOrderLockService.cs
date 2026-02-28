namespace PaymentService.Application.Features.Payments.Services;

public interface IOrderLockService
{
    Task AcquireLockAsync(Guid orderId, CancellationToken cancellationToken);
}
