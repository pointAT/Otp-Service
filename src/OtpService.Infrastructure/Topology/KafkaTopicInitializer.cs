using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpService.Infrastructure.Configuration;

namespace OtpService.Infrastructure.Topology;
//the class that creates the topic at startup
//This is the actual logic. It runs once at startup

//IHostedService runs once at startup and declares broker resources
public sealed class KafkaTopicInitializer : IHostedService
{
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    public KafkaTopicInitializer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaTopicInitializer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = _options.BootstrapServers
        };
        try
        {

            using var admin = new AdminClientBuilder(config).Build();
            // Check existing topics
            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(10));
            if (metadata.Topics.Any(t => t.Topic == _options.OtpRequestsTopic))
            {
                _logger.LogInformation($"Topic {_options.OtpRequestsTopic} is already initialized");
                return;
            }
            

            // Create topic
            await admin.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = _options.OtpRequestsTopic,
                    NumPartitions = _options.OtpRequestsPartitions,
                    ReplicationFactor = 1,

                }

            });
            _logger.LogInformation($"Topic {_options.OtpRequestsTopic} is initialized");
        } 
        catch (CreateTopicsException ex)
        {
            if (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                _logger.LogWarning("Topic already exists: {Topic}", _options.OtpRequestsTopic);
                return;
            }
            _logger.LogError(ex, "Failed to create Kafka topic {Topic}", _options.OtpRequestsTopic);
            throw; 
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initializing Kafka topic.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}