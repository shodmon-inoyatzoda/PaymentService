using PaymentService.Domain.Common;

namespace PaymentService.Domain.ValueObjects;

public sealed class PasswordHash : ValueObject
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;
    private const int WorkFactor = 12;

    public string Hash { get; }

    private PasswordHash(string hash)
    {
        Hash = hash;
    }

    public static Result<PasswordHash> Create(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure<PasswordHash>(
                Error.Validation("Password.Empty", "Password cannot be empty"));
        }

        if (password.Length < MinPasswordLength)
        {
            return Result.Failure<PasswordHash>(
                Error.Validation("Password.TooShort", $"Password must be at least {MinPasswordLength} characters"));
        }

        if (password.Length > MaxPasswordLength)
        {
            return Result.Failure<PasswordHash>(
                Error.Validation("Password.TooLong", $"Password cannot exceed {MaxPasswordLength} characters"));
        }

        // Проверка сложности пароля
        if (!HasUpperCase(password))
        {
            return Result.Failure<PasswordHash>(
                Error.Validation("Password.NoUpperCase", "Password must contain at least one uppercase letter"));
        }

        if (!HasLowerCase(password))
        {
            return Result.Failure<PasswordHash>(
                Error.Validation("Password.NoLowerCase", "Password must contain at least one lowercase letter"));
        }

        if (!HasDigit(password))
        {
            return Result.Failure<PasswordHash>(
                Error.Validation("Password.NoDigit", "Password must contain at least one digit"));
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

        return Result.Success(new PasswordHash(hash));
    }

    public static PasswordHash FromHash(string hash)
    {
        return new PasswordHash(hash);
    }

    public bool Verify(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, Hash);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasUpperCase(string password) =>
        password.Any(char.IsUpper);

    private static bool HasLowerCase(string password) =>
        password.Any(char.IsLower);

    private static bool HasDigit(string password) =>
        password.Any(char.IsDigit);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Hash;
    }

    public override string ToString() => "***PROTECTED***";
}
