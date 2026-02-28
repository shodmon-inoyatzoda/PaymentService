using System.Text.RegularExpressions;
using PaymentService.Domain.Common;

namespace PaymentService.Domain.ValueObjects;

public sealed class PhoneNumber : ValueObject
{
    private const int MinLength = 10;
    private const int MaxLength = 15;

    // Todo: Только таджикские номера
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9\s\-\(\)]+$", RegexOptions.Compiled);

    public string Value { get; }

    public string FormattedValue { get; }

    private PhoneNumber(string value, string formattedValue)
    {
        Value = value;
        FormattedValue = formattedValue;
    }

    public static Result<PhoneNumber> Create(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation("PhoneNumber.Empty", "Phone number cannot be empty"));
        }

        phoneNumber = phoneNumber.Trim();

        if (!PhoneRegex.IsMatch(phoneNumber))
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation("PhoneNumber.InvalidFormat", "Phone number contains invalid characters"));
        }

        var normalized = Regex.Replace(phoneNumber, @"[^\d+]", "");

        var digitsOnly = normalized.Replace("+", "");
        if (digitsOnly.Length < MinLength)
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation(
                    "PhoneNumber.TooShort",
                    $"Phone number must contain at least {MinLength} digits"));
        }

        if (digitsOnly.Length > MaxLength)
        {
            return Result.Failure<PhoneNumber>(
                Error.Validation(
                    "PhoneNumber.TooLong",
                    $"Phone number cannot contain more than {MaxLength} digits"));
        }

        return Result.Success(new PhoneNumber(normalized, phoneNumber));
    }

    public static bool TryCreate(string? phoneNumber, out PhoneNumber? result)
    {
        var createResult = Create(phoneNumber);
        result = createResult.IsSuccess ? createResult.Value : null;
        return createResult.IsSuccess;
    }

    public static bool IsValid(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        phoneNumber = phoneNumber.Trim();

        if (!PhoneRegex.IsMatch(phoneNumber))
            return false;

        var normalized = Regex.Replace(phoneNumber, @"[^\d+]", "");
        var digitsOnly = normalized.Replace("+", "");

        return digitsOnly.Length >= MinLength && digitsOnly.Length <= MaxLength;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => FormattedValue;

    public static implicit operator string?(PhoneNumber? phoneNumber) => phoneNumber?.Value;
}