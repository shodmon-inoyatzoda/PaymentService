using System.Security.Cryptography;
using PaymentService.Application.Auth.Interfaces;

namespace PaymentService.Infrastructure.Auth;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    private const int ByteCount = 64;

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(ByteCount);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
