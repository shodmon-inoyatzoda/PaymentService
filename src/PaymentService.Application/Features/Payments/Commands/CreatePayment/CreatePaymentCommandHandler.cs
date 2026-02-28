using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Common;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Enums.Orders;

namespace PaymentService.Application.Features.Payments.Commands.CreatePayment;

public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Result<PaymentDto>>
{
    private readonly IApplicationDbContext _db;

    public CreatePaymentCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PaymentDto>> Handle(
        CreatePaymentCommand command,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<PaymentDto>(
                Error.NotFound("Order.NotFound", $"Order '{command.OrderId}' was not found."));

        if (order.UserId != command.UserId)
            return Result.Failure<PaymentDto>(
                Error.NotFound("Order.NotFound", $"Order '{command.OrderId}' was not found."));

        if (order.Status != OrderStatus.Created)
            return Result.Failure<PaymentDto>(
                Error.Conflict("Order.Status", "Cannot initiate payment for an order that is not in 'Created' status."));

        var paymentResult = Payment.Create(order.Id, command.UserId, order.Amount, order.Currency);
        if (paymentResult.IsFailure)
            return Result.Failure<PaymentDto>(paymentResult.Error);

        var payment = paymentResult.Value;

        var addResult = order.AddPayment(payment);
        if (addResult.IsFailure)
            return Result.Failure<PaymentDto>(addResult.Error);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(payment));
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
