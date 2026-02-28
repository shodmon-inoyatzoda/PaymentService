namespace PaymentService.Application.Idempotency;

public class IdempotencyKey
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public int ResponseStatusCode { get; private set; }
    public string ResponseBody { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; }

    protected IdempotencyKey()
    {
    }

    public static IdempotencyKey Create(
        Guid userId,
        string key,
        string requestHash,
        int responseStatusCode,
        string responseBody,
        DateTimeOffset expiresAt)
    {
        return new IdempotencyKey
        {
            UserId = userId,
            Key = key,
            RequestHash = requestHash,
            ResponseStatusCode = responseStatusCode,
            ResponseBody = responseBody,
            ExpiresAt = expiresAt
        };
    }
}
