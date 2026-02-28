using MediatR;
using PaymentService.Application.Features.Orders.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    Guid UserId,
    decimal Amount,
    string Currency) : IRequest<Result<OrderDto>>;
