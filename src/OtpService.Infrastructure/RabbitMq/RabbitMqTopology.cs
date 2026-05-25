namespace OtpService.Infrastructure.RabbitMq;


public static class RabbitMqTopology
{
    public const string DeliveryExchange = "otp.delivery";
    public const string DeliveryQueue = "otp.delivery.queue";
    public const string DeliveryRoutingKey = "send";

    // Dead-letter 
    public const string DeadLetterExchange = "otp.dlx";
    public const string DeadLetterQueue = "otp.dlq";
    public const string DeadLetterRoutingKey = "dead";

    // Retry tiers - - - -  - - - queue name + TTL in milliseconds 
    public static readonly (string QueueName, int TtlMs)[] RetryTiers = new[]
    {
        ("otp.retry.5s",    5_000),
        ("otp.retry.30s",   30_000),
        ("otp.retry.2m",    120_000),
        ("otp.retry.10m",   600_000),
        ("otp.retry.30m",   1_800_000)
    };

    // ─── Limits ──────────────────────────────────────────────
    public const byte MaxPriority = 10;
}