namespace PaymentService.Api.Contracts;

public sealed record CreateOrderRequest(decimal Amount, string Currency);
