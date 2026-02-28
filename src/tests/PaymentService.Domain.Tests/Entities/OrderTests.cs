using FluentAssertions;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities.Orders;
using PaymentService.Domain.Entities.Payments;
using PaymentService.Domain.Enums.Orders;
using PaymentService.Domain.Events;

namespace PaymentService.Domain.Tests.Entities;

public class OrderTests
{
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private const decimal ValidAmount = 100m;
    private const string ValidCurrency = "USD";

    // --- Create validation ---

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        var result = Order.Create(ValidUserId, ValidAmount, ValidCurrency);
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(ValidUserId);
        result.Value.Amount.Should().Be(ValidAmount);
        result.Value.Currency.Should().Be(ValidCurrency);
        result.Value.Status.Should().Be(OrderStatus.Created);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldFail()
    {
        var result = Order.Create(Guid.Empty, ValidAmount, ValidCurrency);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldFail()
    {
        var result = Order.Create(ValidUserId, 0m, ValidCurrency);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldFail()
    {
        var result = Order.Create(ValidUserId, -10m, ValidCurrency);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldFail()
    {
        var result = Order.Create(ValidUserId, ValidAmount, "");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithInvalidCurrencyFormat_ShouldFail()
    {
        var result = Order.Create(ValidUserId, ValidAmount, "US");
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_NormalizesLowercaseCurrency()
    {
        var result = Order.Create(ValidUserId, ValidAmount, "usd");
        result.IsSuccess.Should().BeTrue();
        result.Value.Currency.Should().Be("USD");
    }

    // --- MarkAsPaid ---

    [Fact]
    public void MarkAsPaid_FromCreated_ShouldSucceed()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        var result = order.MarkAsPaid();
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void MarkAsPaid_FromPaid_ShouldFail()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        order.MarkAsPaid();
        var result = order.MarkAsPaid();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void MarkAsPaid_FromCancelled_ShouldFail()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        order.MarkAsCancelled();
        var result = order.MarkAsPaid();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void MarkAsPaid_ShouldRaiseOrderPaidDomainEvent()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        order.MarkAsPaid();
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidDomainEvent);
        var evt = (OrderPaidDomainEvent)order.DomainEvents.Single();
        evt.OrderId.Should().Be(order.Id);
    }

    // --- MarkAsCancelled ---

    [Fact]
    public void MarkAsCancelled_FromCreated_ShouldSucceed()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        var result = order.MarkAsCancelled();
        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void MarkAsCancelled_FromPaid_ShouldFail()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        order.MarkAsPaid();
        var result = order.MarkAsCancelled();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    // --- AddPayment ---

    [Fact]
    public void AddPayment_WithMatchingOrderId_ShouldSucceed()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        var payment = CreatePaymentForOrder(order.Id);
        var result = order.AddPayment(payment);
        result.IsSuccess.Should().BeTrue();
        order.Payments.Should().ContainSingle();
    }

    [Fact]
    public void AddPayment_WithMismatchedOrderId_ShouldFail()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        var payment = CreatePaymentForOrder(Guid.NewGuid());
        var result = order.AddPayment(payment);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void AddPayment_WhenOrderIsPaid_ShouldFail()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        order.MarkAsPaid();
        var payment = CreatePaymentForOrder(order.Id);
        var result = order.AddPayment(payment);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void AddPayment_WhenOrderIsCancelled_ShouldFail()
    {
        var order = Order.Create(ValidUserId, ValidAmount, ValidCurrency).Value;
        order.MarkAsCancelled();
        var payment = CreatePaymentForOrder(order.Id);
        var result = order.AddPayment(payment);
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    private static Payment CreatePaymentForOrder(Guid orderId)
    {
        return Payment.Create(orderId, Guid.NewGuid(), ValidAmount, ValidCurrency).Value;
    }
}
