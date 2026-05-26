using System.Text.Json;
using Confluent.Kafka;

using Microsoft.Extensions.Options;
using OtpService.Core.Abstractions;
using OtpService.Core.Configuration;
using OtpService.Core.Contracts;
using OtpService.Core.Hashing;
using OtpService.Core.Models;
using OtpService.Infrastructure.Configuration;
using OtpService.Infrastructure.Persistence;
using OtpService.Infrastructure.RabbitMq;

namespace OtpService.Ingestion.Consumers;

// Consumes OtpRequestMessage from the otp.requests Kafka topic and
// drives the Ingestion pipeline: generate OTP → persist → publish to RabbitMQ → commit offset.
// At-least-once delivery: offset is committed ""AFTER"" the RabbitMQ publish is acked.
public sealed class OtpRequestsConsumer : BackgroundService
{
    private readonly KafkaOptions _kafkaOptions;
    private readonly OtpOptions _otpOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOtpCodeGenerator _codeGenerator;
    private readonly IOtpHasher _hasher;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<OtpRequestsConsumer> _logger;
    private readonly IDedupeStore _dedupe;

    public OtpRequestsConsumer(
        IOptions<KafkaOptions> kafkaOptions,
        IOptions<OtpOptions> otpOptions,
        IServiceScopeFactory scopeFactory,
        IOtpCodeGenerator codeGenerator,
        IOtpHasher hasher,
        IRabbitMqPublisher publisher,
        ILogger<OtpRequestsConsumer> logger, IDedupeStore dedupe)
    {
        _kafkaOptions = kafkaOptions.Value;
        _otpOptions = otpOptions.Value;
        _scopeFactory = scopeFactory;
        _codeGenerator = codeGenerator;
        _hasher = hasher;
        _publisher = publisher;
        _logger = logger;
        _dedupe = dedupe;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = _kafkaOptions.ConsumerGroupId,
            AutoOffsetReset = Enum.Parse<AutoOffsetReset>(_kafkaOptions.AutoOffsetReset, ignoreCase: true),
            EnableAutoCommit = false,
            ClientId = "otp-ingestion"
        };

        
        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_kafkaOptions.OtpRequestsTopic);
        
        _logger.LogInformation(
            "Subscribed to Kafka topic {Topic} (consumer group {GroupId})",
            _kafkaOptions.OtpRequestsTopic, _kafkaOptions.ConsumerGroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Block for up to 1 second waiting for a message. Returns null if none.
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                // Hand off the actual processing to a separate method .
                await ProcessMessageAsync(result, stoppingToken);
                

                // Commit AFTER everything succeeded. This is the at-least-once .
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
              

                // Small delay to avoid hot-looping on a poison message that always fails.
                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
        }

        consumer.Close();   // sends a graceful leave to the consumer group
        _logger.LogInformation("Kafka consumer stopped");
    }

    private async Task ProcessMessageAsync(
        ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        
        var request = JsonSerializer.Deserialize<OtpRequestMessage>(result.Message.Value);
        // Redis SETNX atomically reserves this request
        var dedupeTtl = TimeSpan.FromSeconds(_otpOptions.TtlSeconds * 2);
        var reserved = await _dedupe.TryReserveAsync(
            key: request.RequestId,
            ttl: dedupeTtl,
            cancellationToken: cancellationToken);
        if (!reserved)
        {
            _logger.LogInformation(
                "Duplicate request {RequestId} "
                ,
                request.RequestId);
            return;   // commit the offset and move on 
        }


        var code = _codeGenerator.Generate();
        var salt = _hasher.GenerateSalt();
        var codeHash = _hasher.Hash(code, salt);

       
        var record = new OtpRecord
        {
            RequestId = request.RequestId,
            Msisdn = request.Msisdn,
            Purpose = request.Purpose,
            Priority = OtpPriority.Normal,
            CodeHash = codeHash,
            Salt = salt,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(_otpOptions.TtlSeconds),
            MaxAttempts = _otpOptions.MaxVerifyAttempts
        };

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OtpDbContext>();
            db.OtpRecords.Add(record);
            await db.SaveChangesAsync(cancellationToken);
        }

        var job = new SendOtpJob(
            TrackingId: record.TrackingId,
            Msisdn: request.Msisdn,
            Code: code,                // plaintext, in-transit only
            Purpose: request.Purpose,
            Attempt: 1
        );

        await _publisher.PublishSendJobAsync(job, priority: 5, cancellationToken);

        _logger.LogInformation(
            "Queued OTP {TrackingId} for {Msisdn} (purpose {Purpose})",
            record.TrackingId, request.Msisdn, request.Purpose);
    }
}