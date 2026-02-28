using MediatR;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Payments.Commands.CreatePayment;

public sealed record CreatePaymentCommand(
    Guid UserId,
    Guid OrderId) : IRequest<Result<PaymentDto>>;
