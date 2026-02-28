using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Orders;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Enums.Payments;
using PaymentService.Domain.Events;
using PaymentService.Domain.ValueObjects;

namespace PaymentService.Domain.Entities.Payments;

public class Payment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Money Money { get; private set; } = null!;
    public decimal Amount => Money.Amount;
    public string Currency => Money.Currency;
    public PaymentStatus Status { get; private set; }

    public Order Order { get; private set; } = null!;
    public User User { get; private set; } = null!;

    protected Payment()
    {
    }

    private Payment(Guid orderId, Guid userId, Money money)
    {
        OrderId = orderId;
        UserId = userId;
        Money = money;
        Status = PaymentStatus.Pending;
    }

    public static Result<Payment> Create(Guid orderId, Guid userId, decimal amount, string currency)
    {
        if (orderId == Guid.Empty)
            return Result.Failure<Payment>(Error.Validation("Payment.OrderId.Empty", "OrderId cannot be empty"));

        if (userId == Guid.Empty)
            return Result.Failure<Payment>(Error.Validation("Payment.UserId.Empty", "UserId cannot be empty"));

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsFailure)
            return Result.Failure<Payment>(moneyResult.Error);

        var payment = new Payment(orderId, userId, moneyResult.Value);
        return Result.Success(payment);
    }

    public Result MarkAsCompleted()
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(Error.Conflict("Payment.Status", "Only payments in 'Pending' status can be marked as completed."));
        }

        Status = PaymentStatus.Successful;
        UpdateTimestamp();
        AddDomainEvent(new PaymentSucceededDomainEvent(Id, OrderId));
        return Result.Success();
    }

    public Result MarkAsFailed()
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(Error.Conflict("Payment.Status", "Only payments in 'Pending' status can be marked as failed."));
        }

        Status = PaymentStatus.Failed;
        UpdateTimestamp();
        return Result.Success();
    }
}