using OtpService.Core.Contracts;

namespace OtpService.Infrastructure.RabbitMq;

// Publishes SendOtpJob messages to the RabbitMQ delivery exchange

public interface IRabbitMqPublisher
{
    Task PublishSendJobAsync(SendOtpJob job, byte priority, CancellationToken cancellationToken);
}