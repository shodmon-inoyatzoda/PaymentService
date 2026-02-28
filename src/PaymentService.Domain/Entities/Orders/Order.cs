using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Enums.Orders;

namespace PaymentService.Domain.Entities.Orders;

public class Order : BaseEntity
{
    private readonly List<Payment> _payments = [];
    
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public OrderStatus Status { get; private set; }

    public User User { get; private set; } = null!;
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    public Order()
    {
    }

    private Order(Guid userId, decimal amount, string currency, OrderStatus status)
    {
        UserId = userId;
        Amount = amount;
        Currency = currency;
        Status = status;
    }

    public static Result<Order> Create(Guid userId, decimal amount, string currency)
    {
        var order = new Order
        {
            UserId = userId,
            Amount = amount,
            Currency = currency,
            Status = OrderStatus.Created
        };

        return Result.Success(order);
    }

    public Result MarkAsPaid()
    {
        if (Status != OrderStatus.Created)
        {
            return Result.Failure(Error.Conflict("Order.Status", "Order can only be marked as paid if it is in 'Created' status."));
        }

        Status = OrderStatus.Paid;
        return Result.Success();
    }
    
    public Result MarkAsCancelled()
    {
        if (Status != OrderStatus.Created)
        {
            return Result.Failure(Error.Conflict("Order.Status", "Only orders in 'Created' status can be cancelled."));
        }

        Status = OrderStatus.Cancelled;
        return Result.Success();
    }
    
    public Result AddPayment(Payment payment)
    {
        if (payment.OrderId != Id)
        {
            return Result.Failure(Error.Validation("Payment.OrderId", "Payment does not belong to this order."));
        }

        _payments.Add(payment);
        return Result.Success();
    }
}