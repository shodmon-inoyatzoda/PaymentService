using FluentAssertions;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Enums.Payments;
using PaymentService.Domain.Events;

namespace PaymentService.Domain.Tests.Entities;

public class PaymentTests
{
    private static readonly Guid ValidOrderId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private const decimal ValidAmount = 100m;
    private const string ValidCurrency = "USD";

    // --- Create validation ---

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var result = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency);
        result.IsSuccess.Should().BeTrue();
        result.Value.OrderId.Should().Be(ValidOrderId);
        result.Value.UserId.Should().Be(ValidUserId);
        result.Value.Amount.Should().Be(ValidAmount);
        result.Value.Currency.Should().Be(ValidCurrency);
        result.Value.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void Create_WithEmptyOrderId_ShouldFail()
    {
        var result = Payment.Create(Guid.Empty, ValidUserId, ValidAmount, ValidCurrency);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldFail()
    {
        var result = Payment.Create(ValidOrderId, Guid.Empty, ValidAmount, ValidCurrency);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldFail()
    {
        var result = Payment.Create(ValidOrderId, ValidUserId, 0m, ValidCurrency);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldFail()
    {
        var result = Payment.Create(ValidOrderId, ValidUserId, -5m, ValidCurrency);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithInvalidCurrency_ShouldFail()
    {
        var result = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, "X");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    // --- MarkAsCompleted ---

    [Fact]
    public void MarkAsCompleted_FromPending_ShouldSucceed()
    {
        var payment = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency).Value;
        var result = payment.MarkAsCompleted();
        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Successful);
    }

    [Fact]
    public void MarkAsCompleted_FromFailed_ShouldFail()
    {
        var payment = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency).Value;
        payment.MarkAsFailed();
        var result = payment.MarkAsCompleted();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void MarkAsCompleted_FromSuccessful_ShouldFail()
    {
        var payment = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency).Value;
        payment.MarkAsCompleted();
        var result = payment.MarkAsCompleted();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void MarkAsCompleted_ShouldRaisePaymentSucceededDomainEvent()
    {
        var payment = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency).Value;
        payment.MarkAsCompleted();
        payment.DomainEvents.Should().ContainSingle(e => e is PaymentSucceededDomainEvent);
        var evt = (PaymentSucceededDomainEvent)payment.DomainEvents.Single();
        evt.PaymentId.Should().Be(payment.Id);
        evt.OrderId.Should().Be(ValidOrderId);
    }

    // --- MarkAsFailed ---

    [Fact]
    public void MarkAsFailed_FromPending_ShouldSucceed()
    {
        var payment = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency).Value;
        var result = payment.MarkAsFailed();
        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void MarkAsFailed_FromSuccessful_ShouldFail()
    {
        var payment = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency).Value;
        payment.MarkAsCompleted();
        var result = payment.MarkAsFailed();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void MarkAsFailed_ShouldNotRaiseDomainEvent()
    {
        var payment = Payment.Create(ValidOrderId, ValidUserId, ValidAmount, ValidCurrency).Value;
        payment.MarkAsFailed();
        payment.DomainEvents.Should().BeEmpty();
    }
}
