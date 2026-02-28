using MediatR;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Payments.Queries.GetPaymentsByOrder;

public sealed record GetPaymentsByOrderQuery(
    Guid UserId,
    Guid OrderId) : IRequest<Result<IReadOnlyList<PaymentDto>>>;
