using MediatR;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string IpAddress) : IRequest<Result<AuthResponse>>;
