using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpService.Core.Contracts;
using OtpService.Core.Models;
using OtpService.Infrastructure.Configuration;
using OtpService.Infrastructure.Persistence;
using OtpService.Infrastructure.RabbitMq;
using OtpService.Providers.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OtpService.Delivery.Consumers;


public sealed class OtpDeliveryConsumer : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWhatsAppProvider _provider;
    private readonly ILogger<OtpDeliveryConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public OtpDeliveryConsumer(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        IWhatsAppProvider provider,
        ILogger<OtpDeliveryConsumer> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _provider = provider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Open the RabbitMQ connection and channel 
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(30)
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 10,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
           
            try
            {
                await HandleMessageAsync(ea, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled error processing delivery (DeliveryTag {Tag})",
                    ea.DeliveryTag);

                
                try
                {
                    await _channel!.BasicNackAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: stoppingToken);
                }
                catch
                {
                    
                }
            }
        };

        await _channel.BasicConsumeAsync(
            queue: RabbitMqTopology.DeliveryQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Delivery consumer subscribed to {Queue}",
            RabbitMqTopology.DeliveryQueue);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            
        }

        _logger.LogInformation("Delivery consumer stopping");
    }

    private async Task HandleMessageAsync(
        BasicDeliverEventArgs ea,
        CancellationToken cancellationToken)
    {
        
        var channel = _channel ?? throw new InvalidOperationException(
            "Channel not initialized ");

        SendOtpJob? job;
        try
        {
            var bodyText = Encoding.UTF8.GetString(ea.Body.Span);
            job = JsonSerializer.Deserialize<SendOtpJob>(bodyText);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize delivery message — discarding");
            
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
            return;
        }

        if (job is null)
        {
            _logger.LogWarning("Deserialized job was null — discarding");
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
            return;
        }

        var result = await _provider.SendAsync(job, cancellationToken);

        // Update the OtpRecord in Postgres 
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OtpDbContext>();

        var record = await db.OtpRecords
            .FirstOrDefaultAsync(r => r.TrackingId == job.TrackingId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning(
                "No OtpRecord found for {TrackingId} — acking and skipping",
                job.TrackingId);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
            return;
        }

        if (result.Success)
        {
            record.Status = OtpStatus.Sent;
            record.WhatsAppMessageId = result.WhatsAppMessageId;
        }
        else
        {
           
            record.Status = OtpStatus.Failed;
            record.FailureReason = result.ErrorMessage;
        }

        await db.SaveChangesAsync(cancellationToken);

        // ACK the RabbitMQ message 
      
        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);

        _logger.LogInformation(
            "Processed delivery for {TrackingId} → {Status} ({WhatsAppMessageId})",
            job.TrackingId, record.Status, record.WhatsAppMessageId ?? "n/a");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);
    }
}