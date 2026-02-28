namespace PaymentService.Application.Auth.DTOs;

public sealed record CurrentUserResponse(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string? Email);
