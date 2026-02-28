using PaymentService.Domain.Entities.Users;

namespace PaymentService.Application.Auth.Interfaces;

public interface IJwtTokenService
{
    string CreateAccessToken(User user);
}
