using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Orders;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Enums.Payments;

namespace PaymentService.Domain.Entities.Payments;

public class Payment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }

    public Order Order { get; private set; } = null!;
    public User User { get; private set; } = null!;
    
    public Payment()
    {
    }
    
    private Payment(Guid orderId, Guid userId, decimal amount, PaymentStatus status)
    {
        OrderId = orderId;
        UserId = userId;
        Amount = amount;
        Status = status;
    }
    
    public static Result<Payment> Create(Guid orderId, Guid userId, decimal amount)
    {
        var payment = new Payment(orderId, userId, amount, PaymentStatus.Pending);
        
        return Result.Success(payment);
    }
    
    public Result MarkAsCompleted()
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(Error.Conflict("Payment.Status", "Only payments in 'Pending' status can be marked as completed."));
        }

        Status = PaymentStatus.Successful;
        return Result.Success();
    }
    
    public Result MarkAsFailed()
    {
        if (Status != PaymentStatus.Pending)
        {
            return Result.Failure(Error.Conflict("Payment.Status", "Only payments in 'Pending' status can be marked as failed."));
        }

        Status = PaymentStatus.Failed;
        return Result.Success();
    }
}