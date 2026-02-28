using MediatR;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<CurrentUserResponse>>;
