using PaymentService.Domain.Common;

namespace PaymentService.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount <= 0)
            return Result.Failure<Money>(Error.Validation("Money.Amount.Invalid", "Amount must be greater than zero"));

        if (string.IsNullOrWhiteSpace(currency))
            return Result.Failure<Money>(Error.Validation("Money.Currency.Empty", "Currency cannot be empty"));

        currency = currency.Trim().ToUpperInvariant();

        if (currency.Length != 3 || !currency.All(char.IsLetter))
            return Result.Failure<Money>(Error.Validation("Money.Currency.Invalid", "Currency must be a 3-letter ISO 4217 code (e.g. USD, EUR)"));

        return Result.Success(new Money(amount, currency));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount} {Currency}";
}
