using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Common;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Users;

namespace PaymentService.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;

    public RegisterCommandHandler(
        IApplicationDbContext db,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
    }

    public async Task<Result<AuthResponse>> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var phoneExists = await _db.Users
            .AnyAsync(u => u.PhoneNumber.Value == command.PhoneNumber, cancellationToken);

        if (phoneExists)
            return Result.Failure<AuthResponse>(
                Error.Conflict("User.PhoneExists", "A user with this phone number already exists."));

        var emailExists = await _db.Users
            .AnyAsync(u => u.Email != null && u.Email.Value == command.Email, cancellationToken);

        if (emailExists)
            return Result.Failure<AuthResponse>(
                Error.Conflict("User.EmailExists", "A user with this email already exists."));

        var userResult = User.Register(
            command.PhoneNumber,
            command.Email,
            command.FullName,
            command.Password);

        if (userResult.IsFailure)
            return Result.Failure<AuthResponse>(userResult.Error);

        var user = userResult.Value;

        _db.Users.Add(user);

        var rawToken = _refreshTokenGenerator.Generate();
        var refreshToken = user.AddRefreshToken(rawToken, command.IpAddress);
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.CreateAccessToken(user);

        return Result.Success(new AuthResponse(accessToken, rawToken, user.Id, user.FullName));
    }
}
