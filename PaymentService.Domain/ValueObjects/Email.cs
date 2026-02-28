using System.Text.RegularExpressions;
using PaymentService.Domain.Common;

namespace PaymentService.Domain.ValueObjects;

public sealed class Email : ValueObject
{
    private const int MaxLength = 100;
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Result<Email> Create(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<Email>(
                Error.Validation(
                    "Email.Empty",
                    "Email address cannot be empty"));
        }

        email = email.Trim().ToLowerInvariant();

        if (email.Length > MaxLength)
        {
            return Result.Failure<Email>(
                Error.Validation(
                    "Email.TooLong",
                    $"Email address cannot be longer than {MaxLength} characters"));
        }

        if (!EmailRegex.IsMatch(email))
        {
            return Result.Failure<Email>(
                Error.Validation(
                    "Email.InvalidFormat",
                    "Email address has invalid format"));
        }

        try
        {
            var mailAddress = new System.Net.Mail.MailAddress(email);
            if (mailAddress.Address != email)
            {
                return Result.Failure<Email>(
                    Error.Validation(
                        "Email.InvalidFormat",
                        "Email address has invalid format"));
            }
        }
        catch
        {
            return Result.Failure<Email>(
                Error.Validation(
                    "Email.InvalidFormat",
                    "Email address has invalid format"));
        }

        return Result.Success(new Email(email));
    }

    public static bool TryCreate(string? email, out Email? result)
    {
        var createResult = Create(email);
        result = createResult.IsSuccess ? createResult.Value : null;
        return createResult.IsSuccess;
    }

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        email = email.Trim().ToLowerInvariant();

        if (email.Length > MaxLength)
            return false;

        return EmailRegex.IsMatch(email);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string?(Email? email) => email?.Value;
}