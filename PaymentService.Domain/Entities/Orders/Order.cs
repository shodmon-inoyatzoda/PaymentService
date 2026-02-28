using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Users;
using PaymentService.Domain.Enums.Orders;

namespace PaymentService.Domain.Entities.Orders;

public class Order : BaseEntity
{
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public OrderStatus Status { get; set; }

    public User User { get; set; } = null!;
}