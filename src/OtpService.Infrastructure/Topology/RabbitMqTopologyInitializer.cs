using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpService.Infrastructure.Configuration;
using OtpService.Infrastructure.RabbitMq;
using RabbitMQ.Client;

namespace OtpService.Infrastructure.Topology;

public sealed class RabbitMqTopologyInitializer : IHostedService
{
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;
    private readonly RabbitMqOptions _options;

    public RabbitMqTopologyInitializer(
        ILogger<RabbitMqTopologyInitializer> logger
        , IOptions<RabbitMqOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                ContinuationTimeout = TimeSpan.FromSeconds(30),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30)
            };

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // Dead-letter exchange
            _logger.LogInformation("Declaring Dead-Letter Exchange: {Exchange}", RabbitMqTopology.DeadLetterExchange);
            await channel.ExchangeDeclareAsync(
                exchange: RabbitMqTopology.DeadLetterExchange,
                type: ExchangeType.Fanout, //fanout — broadcasts to all bindings
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            // Dead-letter queue
            _logger.LogInformation("Declaring Dead-Letter Queue: {Queue}", RabbitMqTopology.DeadLetterQueue);
            await channel.QueueDeclareAsync(
                queue: RabbitMqTopology.DeadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Binding Dead-Letter Queue {Queue} to Exchange {Exchange}", RabbitMqTopology.DeadLetterQueue, RabbitMqTopology.DeadLetterExchange);
            await channel.QueueBindAsync(
                queue: RabbitMqTopology.DeadLetterQueue,
                exchange: RabbitMqTopology.DeadLetterExchange,
                routingKey: "",   // fanout ignore routing key
                cancellationToken: cancellationToken);
            
            _logger.LogInformation("Declaring Delivery Exchange: {Exchange}", RabbitMqTopology.DeliveryExchange);
            await channel.ExchangeDeclareAsync(
                exchange: RabbitMqTopology.DeliveryExchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            var deliveryQueueArgs = new Dictionary<string, object?>
            {
                ["x-max-priority"] = (int)RabbitMqTopology.MaxPriority
            };

            _logger.LogInformation("Declaring Delivery Queue: {Queue} with max-priority: {MaxPriority}", RabbitMqTopology.DeliveryQueue, RabbitMqTopology.MaxPriority);
            await channel.QueueDeclareAsync(
                queue: RabbitMqTopology.DeliveryQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: deliveryQueueArgs,
                cancellationToken: cancellationToken);

            _logger.LogDebug("Binding Delivery Queue {Queue} to Exchange {Exchange} with RoutingKey {RoutingKey}", RabbitMqTopology.DeliveryQueue, RabbitMqTopology.DeliveryExchange, RabbitMqTopology.DeliveryRoutingKey);
            await channel.QueueBindAsync(
                queue: RabbitMqTopology.DeliveryQueue,
                exchange: RabbitMqTopology.DeliveryExchange,
                routingKey: RabbitMqTopology.DeliveryRoutingKey,
                cancellationToken: cancellationToken);
            
            foreach (var (queueName, ttlMs) in RabbitMqTopology.RetryTiers)
            {
                var retryQueueArgs = new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = ttlMs,
                    ["x-dead-letter-exchange"] = RabbitMqTopology.DeliveryExchange,
                    ["x-dead-letter-routing-key"] = RabbitMqTopology.DeliveryRoutingKey
                };

                _logger.LogInformation("Declaring Retry Tier Queue: {Queue} with TTL: {TtlMs}ms", queueName, ttlMs);
                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: retryQueueArgs,
                    cancellationToken: cancellationToken);
            }

            _logger.LogInformation("RabbitMQ topology initialization completed successfully.");
        }
      
      
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while initializing RabbitMQ topology.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    
        => Task.CompletedTask;
    
}