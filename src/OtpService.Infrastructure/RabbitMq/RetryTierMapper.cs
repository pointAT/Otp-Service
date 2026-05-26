namespace OtpService.Infrastructure.RabbitMq;


public static class RetryTierMapper
{
    public const int MaxAttempts = 6;   // 1 initial + 5 retries

    
    public static string? GetRetryQueueForNextAttempt(int completedAttempt)
    {
      
        var tierIndex = completedAttempt - 1;

        if (tierIndex < 0 || tierIndex >= RabbitMqTopology.RetryTiers.Length)
            return null;

        return RabbitMqTopology.RetryTiers[tierIndex].QueueName;
    }
}