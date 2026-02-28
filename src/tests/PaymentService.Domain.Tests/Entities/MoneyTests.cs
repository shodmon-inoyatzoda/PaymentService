using FluentAssertions;
using PaymentService.Domain.Common;
using PaymentService.Domain.ValueObjects;

namespace PaymentService.Domain.Tests.Entities;

public class MoneyTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var result = Money.Create(100m, "USD");
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(100m);
        result.Value.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_NormalizesLowercaseCurrency()
    {
        var result = Money.Create(50m, "eur");
        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldFail()
    {
        var result = Money.Create(0m, "USD");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldFail()
    {
        var result = Money.Create(-1m, "USD");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldFail()
    {
        var result = Money.Create(100m, "");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithTwoLetterCurrency_ShouldFail()
    {
        var result = Money.Create(100m, "US");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithFourLetterCurrency_ShouldFail()
    {
        var result = Money.Create(100m, "USDD");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithNumericCurrency_ShouldFail()
    {
        var result = Money.Create(100m, "123");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_ShouldBeEqual()
    {
        var money1 = Money.Create(100m, "USD").Value;
        var money2 = Money.Create(100m, "USD").Value;
        money1.Should().Be(money2);
    }

    [Fact]
    public void Equality_DifferentCurrency_ShouldNotBeEqual()
    {
        var money1 = Money.Create(100m, "USD").Value;
        var money2 = Money.Create(100m, "EUR").Value;
        money1.Should().NotBe(money2);
    }
}
