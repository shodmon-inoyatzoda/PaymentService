using MediatR;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(
    string PhoneNumber,
    string Password,
    string IpAddress) : IRequest<Result<AuthResponse>>;
