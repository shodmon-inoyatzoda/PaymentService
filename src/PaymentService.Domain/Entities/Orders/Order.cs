using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Enums.Orders;
using PaymentService.Domain.Events;
using PaymentService.Domain.ValueObjects;

namespace PaymentService.Domain.Entities.Orders;

public class Order : BaseEntity
{
    private readonly List<Payment> _payments = [];

    public Guid UserId { get; private set; }
    public Money Money { get; private set; } = null!;
    public decimal Amount => Money.Amount;
    public string Currency => Money.Currency;
    public OrderStatus Status { get; private set; }

    public User User { get; private set; } = null!;
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    protected Order()
    {
    }

    private Order(Guid userId, Money money)
    {
        UserId = userId;
        Money = money;
        Status = OrderStatus.Created;
    }

    public static Result<Order> Create(Guid userId, decimal amount, string currency)
    {
        if (userId == Guid.Empty)
            return Result.Failure<Order>(Error.Validation("Order.UserId.Empty", "UserId cannot be empty"));

        var moneyResult = Money.Create(amount, currency);
        if (moneyResult.IsFailure)
            return Result.Failure<Order>(moneyResult.Error);

        var order = new Order(userId, moneyResult.Value);
        return Result.Success(order);
    }

    public Result MarkAsPaid()
    {
        if (Status != OrderStatus.Created)
        {
            return Result.Failure(Error.Conflict("Order.Status", "Order can only be marked as paid if it is in 'Created' status."));
        }

        Status = OrderStatus.Paid;
        UpdateTimestamp();
        AddDomainEvent(new OrderPaidDomainEvent(Id));
        return Result.Success();
    }

    public Result MarkAsCancelled()
    {
        if (Status != OrderStatus.Created)
        {
            return Result.Failure(Error.Conflict("Order.Status", "Only orders in 'Created' status can be cancelled."));
        }

        Status = OrderStatus.Cancelled;
        UpdateTimestamp();
        return Result.Success();
    }

    public Result AddPayment(Payment payment)
    {
        if (payment.OrderId != Id)
        {
            return Result.Failure(Error.Validation("Payment.OrderId", "Payment does not belong to this order."));
        }

        if (Status != OrderStatus.Created)
        {
            return Result.Failure(Error.Conflict("Order.Status", "Cannot add payments to an order that is not in 'Created' status."));
        }

        _payments.Add(payment);
        return Result.Success();
    }
}