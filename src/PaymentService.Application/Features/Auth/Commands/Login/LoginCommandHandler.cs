using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Common;
using PaymentService.Domain.Common;

namespace PaymentService.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;

    public LoginCommandHandler(
        IApplicationDbContext db,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
    }

    public async Task<Result<AuthResponse>> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.PhoneNumber.Value == command.PhoneNumber, cancellationToken);

        if (user is null || !user.VerifyPassword(command.Password))
            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Auth.InvalidCredentials", "Invalid phone number or password."));

        user.RecordLogin(command.IpAddress);

        var rawToken = _refreshTokenGenerator.Generate();
        var refreshToken = user.AddRefreshToken(rawToken, command.IpAddress);
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.CreateAccessToken(user);

        return Result.Success(new AuthResponse(accessToken, rawToken, user.Id, user.FullName));
    }
}
