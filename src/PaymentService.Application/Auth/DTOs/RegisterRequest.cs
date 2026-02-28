namespace PaymentService.Application.Auth.DTOs;

public sealed record RegisterRequest(
    string PhoneNumber,
    string Email,
    string FullName,
    string Password);
