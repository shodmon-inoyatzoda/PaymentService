using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Common;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;

    public RefreshTokenCommandHandler(
        IApplicationDbContext db,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
    }

    public async Task<Result<AuthResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(
                u => u.RefreshTokens.Any(rt => rt.Token == command.RefreshToken),
                cancellationToken);

        if (user is null)
            return Result.Failure<AuthResponse>(
                Error.Unauthorized("RefreshToken.Invalid", "Invalid refresh token."));

        var newRawToken = _refreshTokenGenerator.Generate();

        var revokeResult = user.RevokeRefreshToken(command.RefreshToken, command.IpAddress, newRawToken);
        if (revokeResult.IsFailure)
            return Result.Failure<AuthResponse>(revokeResult.Error);

        var newRefreshToken = user.AddRefreshToken(newRawToken, command.IpAddress);
        _db.RefreshTokens.Add(newRefreshToken);

        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.CreateAccessToken(user);

        return Result.Success(new AuthResponse(accessToken, newRawToken, user.Id, user.FullName));
    }
}
