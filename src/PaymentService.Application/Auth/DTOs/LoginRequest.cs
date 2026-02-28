namespace PaymentService.Application.Auth.DTOs;

public sealed record LoginRequest(
    string PhoneNumber,
    string Password);
