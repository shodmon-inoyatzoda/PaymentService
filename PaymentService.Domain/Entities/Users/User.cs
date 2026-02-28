using PaymentService.Domain.Common;
using PaymentService.Domain.ValueObjects;

namespace PaymentService.Domain.Entities.Users;

public sealed partial class User : BaseEntity
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    public PhoneNumber PhoneNumber { get; private set; }
    public Email? Email { get; private set; }
    public string FullName { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;

    public DateTime? LastLoginAt { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();


    private User() { }

    private User(
        PhoneNumber phoneNumber,
        Email email,
        string fullName,
        PasswordHash passwordHash)
    {
        PhoneNumber = phoneNumber;
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<User> Register(
        string phoneNumber,
        string email,
        string fullName,
        string password)
    {
        var phoneResult = PhoneNumber.Create(phoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure<User>(phoneResult.Error);

        var emailResult = Email.Create(email);
        if (emailResult.IsFailure)
            return Result.Failure<User>(emailResult.Error);

        var passwordHashResult = PasswordHash.Create(password);
        if (passwordHashResult.IsFailure)
            return Result.Failure<User>(passwordHashResult.Error);

        var user = new User(
            phoneResult.Value,
            emailResult.Value,
            fullName,
            passwordHashResult.Value);

        return Result.Success(user);
    }

    public bool VerifyPassword(string password)
    {
        return PasswordHash.Verify(password);
    }

    public Result ChangePassword(string currentPassword, string newPassword)
    {
        if (!VerifyPassword(currentPassword))
        {
            return Result.Failure(
                Error.Validation("User.Password.Invalid", "Current password is incorrect"));
        }

        var newPasswordHashResult = PasswordHash.Create(newPassword);
        if (newPasswordHashResult.IsFailure)
            return Result.Failure(newPasswordHashResult.Error);

        PasswordHash = newPasswordHashResult.Value;
        UpdateTimestamp();

        return Result.Success();
    }

    public void RecordLogin(string ipAddress)
    {
        LastLoginAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public RefreshToken AddRefreshToken(string token, string ipAddress, int expiresInDays = 7)
    {
        var refreshToken = RefreshToken.Create(Id, token, ipAddress, expiresInDays);
        _refreshTokens.Add(refreshToken);

        RemoveOldRefreshTokens();

        return refreshToken;
    }

    public Result RevokeRefreshToken(string token, string ipAddress, string? replacedByToken = null)
    {
        var refreshToken = _refreshTokens.FirstOrDefault(t => t.Token == token);

        if (refreshToken == null)
        {
            return Result.Failure(
                Error.NotFound("RefreshToken.NotFound", "Refresh token not found"));
        }

        if (!refreshToken.IsActive)
        {
            return Result.Failure(
                Error.Validation("RefreshToken.Inactive", "Refresh token is not active"));
        }

        refreshToken.Revoke(ipAddress, replacedByToken);

        return Result.Success();
    }


    private void RemoveOldRefreshTokens()
    {
        var tokensToRemove = _refreshTokens
            .Where(t => !t.IsActive && t.CreatedAt.AddDays(2) < DateTime.UtcNow)
            .ToList();

        foreach (var token in tokensToRemove)
        {
            _refreshTokens.Remove(token);
        }
    }
}