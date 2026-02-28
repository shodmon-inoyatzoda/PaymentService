using PaymentService.Domain.Enums.Payments;

namespace PaymentService.Application.Features.Payments.DTOs;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTimeOffset CreatedAt);
