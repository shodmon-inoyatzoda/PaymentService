namespace PaymentService.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredOn { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset? ProcessedOn { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
