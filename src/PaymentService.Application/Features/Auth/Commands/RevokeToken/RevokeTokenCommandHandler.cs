using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Common;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Commands.RevokeToken;

public sealed class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public RevokeTokenCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        RevokeTokenCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(
                u => u.RefreshTokens.Any(rt => rt.Token == command.RefreshToken),
                cancellationToken);

        if (user is null)
            return Result.Failure(
                Error.NotFound("RefreshToken.NotFound", "Refresh token not found."));

        var revokeResult = user.RevokeRefreshToken(command.RefreshToken, command.IpAddress);
        if (revokeResult.IsFailure)
            return revokeResult;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
