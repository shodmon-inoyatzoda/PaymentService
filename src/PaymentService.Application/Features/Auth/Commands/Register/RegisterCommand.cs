using MediatR;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Commands.Register;

public sealed record RegisterCommand(
    string PhoneNumber,
    string Email,
    string FullName,
    string Password,
    string IpAddress) : IRequest<Result<AuthResponse>>;
