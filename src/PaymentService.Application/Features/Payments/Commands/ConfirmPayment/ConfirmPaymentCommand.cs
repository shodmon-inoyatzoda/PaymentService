using MediatR;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Payments.Commands.ConfirmPayment;

public sealed record ConfirmPaymentCommand(
    Guid UserId,
    Guid PaymentId) : IRequest<Result<PaymentDto>>;
