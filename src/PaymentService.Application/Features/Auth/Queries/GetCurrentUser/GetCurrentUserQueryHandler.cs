using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Common;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    private readonly IApplicationDbContext _db;

    public GetCurrentUserQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<CurrentUserResponse>> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == query.UserId, cancellationToken);

        if (user is null)
            return Result.Failure<CurrentUserResponse>(
                Error.NotFound("User.NotFound", "User not found."));

        return Result.Success(new CurrentUserResponse(
            user.Id,
            user.FullName,
            user.PhoneNumber.Value,
            user.Email?.Value));
    }
}
