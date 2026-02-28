using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Common;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Payments;

namespace PaymentService.Application.Features.Payments.Queries.GetPaymentsByOrder;

public sealed class GetPaymentsByOrderQueryHandler
    : IRequestHandler<GetPaymentsByOrderQuery, Result<IReadOnlyList<PaymentDto>>>
{
    private readonly IApplicationDbContext _db;

    public GetPaymentsByOrderQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<PaymentDto>>> Handle(
        GetPaymentsByOrderQuery query,
        CancellationToken cancellationToken)
    {
        var orderExists = await _db.Orders
            .AnyAsync(o => o.Id == query.OrderId && o.UserId == query.UserId, cancellationToken);

        if (!orderExists)
            return Result.Failure<IReadOnlyList<PaymentDto>>(
                Error.NotFound("Order.NotFound", $"Order '{query.OrderId}' was not found."));

        var payments = await _db.Payments
            .Where(p => p.OrderId == query.OrderId && p.UserId == query.UserId)
            .ToListAsync(cancellationToken);

        var dtos = payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentDto(
                p.Id,
                p.OrderId,
                p.UserId,
                p.Amount,
                p.Currency,
                p.Status,
                p.CreatedAt))
            .ToList();

        return Result.Success<IReadOnlyList<PaymentDto>>(dtos);
    }
}
