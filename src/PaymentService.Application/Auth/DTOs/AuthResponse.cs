namespace PaymentService.Application.Auth.DTOs;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string FullName);
