using MediatR;
using PaymentService.Application.Features.Orders.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(
    Guid OrderId,
    Guid UserId) : IRequest<Result<OrderDto>>;
