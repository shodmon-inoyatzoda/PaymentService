using MediatR;
using PaymentService.Application.Common;
using PaymentService.Application.Features.Orders.DTOs;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Orders;

namespace PaymentService.Application.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    private readonly IApplicationDbContext _db;

    public CreateOrderCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<OrderDto>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var orderResult = Order.Create(command.UserId, command.Amount, command.Currency);
        if (orderResult.IsFailure)
            return Result.Failure<OrderDto>(orderResult.Error);

        var order = orderResult.Value;
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success(new OrderDto(
            order.Id,
            order.UserId,
            order.Amount,
            order.Currency,
            order.Status,
            order.CreatedAt));
    }
}
