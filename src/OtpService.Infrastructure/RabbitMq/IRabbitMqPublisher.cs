using OtpService.Core.Contracts;

namespace OtpService.Infrastructure.RabbitMq;

// Publishes SendOtpJob messages to the RabbitMQ delivery exchange

public interface IRabbitMqPublisher
{
    Task PublishSendJobAsync(SendOtpJob job, byte priority, CancellationToken cancellationToken);
    // Publish a SendOtpJob to a specific retry-tier queue. The queue's
    // TTL holds the message, then dead-letters it back to the main
    // delivery exchange when the TTL expires.
    Task PublishRetryAsync( Core.Contracts.SendOtpJob job,byte priority, int attempt,string retryQueueName,CancellationToken cancellationToken);
    // Publish a SendOtpJob to the dead-letter exchange (terminal).
    Task PublishDeadLetterAsync( Core.Contracts.SendOtpJob job,string reason,CancellationToken cancellationToken);
}