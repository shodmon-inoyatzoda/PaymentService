namespace PaymentService.Api.Common;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public PolicySettings Global { get; init; } = new();
    public PolicySettings Auth { get; init; } = new();
    public PolicySettings PaymentConfirm { get; init; } = new();

    public sealed class PolicySettings
    {
        public int PermitLimit { get; init; } = 100;
        public int WindowSeconds { get; init; } = 60;
        public int QueueLimit { get; init; } = 0;
    }
}
