using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Common;
using PaymentService.Application.Features.Orders.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Orders.Queries.GetOrderById;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    private readonly IApplicationDbContext _db;

    public GetOrderByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<OrderDto>> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == query.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<OrderDto>(
                Error.NotFound("Order.NotFound", "Order not found."));

        if (order.UserId != query.UserId)
            return Result.Failure<OrderDto>(
                Error.NotFound("Order.NotFound", "Order not found."));

        return Result.Success(new OrderDto(
            order.Id,
            order.UserId,
            order.Amount,
            order.Currency,
            order.Status,
            order.CreatedAt));
    }
}
