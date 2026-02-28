using MediatR;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Commands.RevokeToken;

public sealed record RevokeTokenCommand(
    string RefreshToken,
    string IpAddress) : IRequest<Result>;
