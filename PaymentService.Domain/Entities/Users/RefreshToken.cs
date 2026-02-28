using PaymentService.Domain.Common;

namespace PaymentService.Domain.Entities.Users;

public sealed class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }

    public string Token { get; private set; } = null!;

    public DateTime ExpiresAt { get; private set; }

    public string CreatedByIp { get; private set; } = null!;

    public DateTime? RevokedAt { get; private set; }

    public string? RevokedByIp { get; private set; }

    public string? ReplacedByToken { get; private set; }

    public User User { get; private set; } = null!;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    private RefreshToken() { } // Для EF Core

    private RefreshToken(
        Guid userId,
        string token,
        string createdByIp,
        DateTime expiresAt)
    {
        UserId = userId;
        Token = token;
        CreatedByIp = createdByIp;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static RefreshToken Create(
        Guid userId,
        string token,
        string ipAddress,
        int expiresInDays = 7)
    {
        return new RefreshToken(
            userId,
            token,
            ipAddress,
            DateTime.UtcNow.AddDays(expiresInDays));
    }

    public void Revoke(string ipAddress, string? replacedByToken = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = ipAddress;
        ReplacedByToken = replacedByToken;
        UpdateTimestamp();
    }
}