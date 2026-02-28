using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Common;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Application.Features.Payments.Services;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Enums.Orders;
using PaymentService.Domain.Enums.Payments;

namespace PaymentService.Application.Features.Payments.Commands.ConfirmPayment;

public sealed class ConfirmPaymentCommandHandler : IRequestHandler<ConfirmPaymentCommand, Result<PaymentDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IOrderLockService _lockService;
    private readonly IPaymentProviderClient _providerClient;

    public ConfirmPaymentCommandHandler(
        IApplicationDbContext db,
        IOrderLockService lockService,
        IPaymentProviderClient providerClient)
    {
        _db = db;
        _lockService = lockService;
        _providerClient = providerClient;
    }

    public async Task<Result<PaymentDto>> Handle(
        ConfirmPaymentCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load payment + order without tracking to validate ownership before entering transaction
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == command.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(
                Error.NotFound("Payment.NotFound", $"Payment '{command.PaymentId}' was not found."));

        if (payment.UserId != command.UserId || payment.Order.UserId != command.UserId)
            return Result.Failure<PaymentDto>(
                Error.NotFound("Payment.NotFound", $"Payment '{command.PaymentId}' was not found."));

        // 2. Call provider OUTSIDE the database transaction to keep DB locks short.
        //    The resilient client handles timeout / retry / circuit-breaker internally and
        //    always returns a ProviderChargeResult (never throws).
        var chargeRequest = new ProviderChargeRequest(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.Currency);

        var chargeResult = await _providerClient.ChargeAsync(chargeRequest, cancellationToken);

        if (chargeResult.Status != ProviderChargeStatus.Succeeded)
        {
            // Mark the payment as Failed in a short, lock-free transaction
            await TryMarkPaymentAsFailedAsync(command.PaymentId, cancellationToken);

            return chargeResult.Status switch
            {
                ProviderChargeStatus.Unavailable or ProviderChargeStatus.Timeout =>
                    Result.Failure<PaymentDto>(Error.ServiceUnavailable(
                        "Provider.Unavailable",
                        chargeResult.ErrorMessage ?? "Payment provider is currently unavailable.")),
                _ =>
                    Result.Failure<PaymentDto>(Error.Failure(
                        "Provider.Declined",
                        chargeResult.ErrorMessage ?? "Payment provider declined the charge."))
            };
        }

        // 3. Provider succeeded — finalize atomically (lock + re-validate + update)
        try
        {
            return await ExecuteConfirmAsync(payment.OrderId, command.PaymentId, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Final safety net: partial unique index prevented a double successful payment
            return Result.Failure<PaymentDto>(
                Error.Conflict("Payment.DoublePayment", "Another payment for this order has already been confirmed."));
        }
    }

    /// <summary>
    /// Marks the payment as Failed in a short lock-free transaction.
    /// Safe to call even if the payment has already moved out of Pending status.
    /// </summary>
    private async Task TryMarkPaymentAsFailedAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment?.Status == PaymentStatus.Pending)
        {
            payment.MarkAsFailed();
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
    }

    private async Task<Result<PaymentDto>> ExecuteConfirmAsync(
        Guid orderId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // 3. Acquire row-level lock on the order (SELECT ... FOR UPDATE)
        await _lockService.AcquireLockAsync(orderId, cancellationToken);

        // 4. Re-read entities with tracking after the lock is held to get the freshest state
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        var trackedPayment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        // 5. Re-check invariants inside the transaction
        if (order!.Status != OrderStatus.Created)
            return Result.Failure<PaymentDto>(
                Error.Conflict("Order.Status", "The order has already been paid or cancelled."));

        if (trackedPayment!.Status != PaymentStatus.Pending)
            return Result.Failure<PaymentDto>(
                Error.Conflict("Payment.Status", "The payment is no longer in Pending status."));

        var hasSuccessfulPayment = await _db.Payments
            .AnyAsync(p => p.OrderId == orderId && p.Status == PaymentStatus.Successful, cancellationToken);

        if (hasSuccessfulPayment)
            return Result.Failure<PaymentDto>(
                Error.Conflict("Payment.AlreadyConfirmed", "Another payment for this order has already been confirmed."));

        // 6. Update statuses atomically
        var paymentResult = trackedPayment.MarkAsCompleted();
        if (paymentResult.IsFailure)
            return Result.Failure<PaymentDto>(paymentResult.Error);

        var orderResult = order.MarkAsPaid();
        if (orderResult.IsFailure)
            return Result.Failure<PaymentDto>(orderResult.Error);

        // 7. Save and commit atomically
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Success(ToDto(trackedPayment));
    }

    private static PaymentDto ToDto(Payment payment) => new(
        payment.Id,
        payment.OrderId,
        payment.UserId,
        payment.Amount,
        payment.Currency,
        payment.Status,
        payment.CreatedAt);
}
