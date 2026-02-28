using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Auth.DTOs;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Common;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Users;

namespace PaymentService.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;

    public AuthService(
        IApplicationDbContext db,
        IJwtTokenService jwtTokenService,
        IRefreshTokenGenerator refreshTokenGenerator)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        // Check phone uniqueness
        var phoneExists = await _db.Users
            .AnyAsync(u => u.PhoneNumber.Value == request.PhoneNumber, cancellationToken);

        if (phoneExists)
            return Result.Failure<AuthResponse>(
                Error.Conflict("User.PhoneExists", "A user with this phone number already exists."));

        // Check email uniqueness
        var emailExists = await _db.Users
            .AnyAsync(u => u.Email != null && u.Email.Value == request.Email, cancellationToken);

        if (emailExists)
            return Result.Failure<AuthResponse>(
                Error.Conflict("User.EmailExists", "A user with this email already exists."));

        var userResult = User.Register(
            request.PhoneNumber,
            request.Email,
            request.FullName,
            request.Password);

        if (userResult.IsFailure)
            return Result.Failure<AuthResponse>(userResult.Error);

        var user = userResult.Value;

        _db.Users.Add(user);

        var rawToken = _refreshTokenGenerator.Generate();
        var refreshToken = user.AddRefreshToken(rawToken, ipAddress);

        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.CreateAccessToken(user);

        return Result.Success(new AuthResponse(accessToken, rawToken, user.Id, user.FullName));
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.PhoneNumber.Value == request.PhoneNumber, cancellationToken);

        // Use generic message to avoid user enumeration
        if (user is null || !user.VerifyPassword(request.Password))
            return Result.Failure<AuthResponse>(
                Error.Unauthorized("Auth.InvalidCredentials", "Invalid phone number or password."));

        user.RecordLogin(ipAddress);

        var rawToken = _refreshTokenGenerator.Generate();
        var refreshToken = user.AddRefreshToken(rawToken, ipAddress);
        _db.RefreshTokens.Add(refreshToken); // ensure new entity is tracked as Added

        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.CreateAccessToken(user);

        return Result.Success(new AuthResponse(accessToken, rawToken, user.Id, user.FullName));
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(
        RefreshTokenRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(
                u => u.RefreshTokens.Any(rt => rt.Token == request.RefreshToken),
                cancellationToken);

        if (user is null)
            return Result.Failure<AuthResponse>(
                Error.Unauthorized("RefreshToken.Invalid", "Invalid refresh token."));

        var newRawToken = _refreshTokenGenerator.Generate();

        var revokeResult = user.RevokeRefreshToken(request.RefreshToken, ipAddress, newRawToken);
        if (revokeResult.IsFailure)
            return Result.Failure<AuthResponse>(revokeResult.Error);

        var newRefreshToken = user.AddRefreshToken(newRawToken, ipAddress);
        _db.RefreshTokens.Add(newRefreshToken); // ensure new entity is tracked as Added

        await _db.SaveChangesAsync(cancellationToken);

        var accessToken = _jwtTokenService.CreateAccessToken(user);

        return Result.Success(new AuthResponse(accessToken, newRawToken, user.Id, user.FullName));
    }

    public async Task<Result> RevokeTokenAsync(
        RevokeTokenRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(
                u => u.RefreshTokens.Any(rt => rt.Token == request.RefreshToken),
                cancellationToken);

        if (user is null)
            return Result.Failure(
                Error.NotFound("RefreshToken.NotFound", "Refresh token not found."));

        var revokeResult = user.RevokeRefreshToken(request.RefreshToken, ipAddress);
        if (revokeResult.IsFailure)
            return revokeResult;

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<CurrentUserResponse>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

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
