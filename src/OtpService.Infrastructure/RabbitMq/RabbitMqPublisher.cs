using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpService.Core.Contracts;
using OtpService.Infrastructure.Configuration;
using RabbitMQ.Client;

namespace OtpService.Infrastructure.RabbitMq;


public sealed class RabbitMqPublisher : IRabbitMqPublisher, IHostedService, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(30)
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);

        var channelOpts = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
            
        _channel = await _connection.CreateChannelAsync(channelOpts, cancellationToken);

        _logger.LogInformation("RabbitMQ publisher connected.");

    }

    public async Task PublishSendJobAsync(
        SendOtpJob job,
        byte priority,
        CancellationToken cancellationToken)
    {
        if (_channel is null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized. Did StartAsync run?");

        var json = JsonSerializer.SerializeToUtf8Bytes(job);

        var props = new BasicProperties
        {
            Persistent = true,        // disk-persisted, survives broker restart
            ContentType = "application/json",
            Priority = priority,      // 0-10, used by x-max-priority queue
            MessageId = job.TrackingId.ToString()
        };

        await _channel.BasicPublishAsync(
            exchange: RabbitMqTopology.DeliveryExchange,
            routingKey: RabbitMqTopology.DeliveryRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: json,
            cancellationToken: cancellationToken);

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) 
            await _channel.CloseAsync(cancellationToken);
            
        if (_connection is not null) 
            await _connection.CloseAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) 
            await _channel.DisposeAsync();
            
        if (_connection is not null) 
            await _connection.DisposeAsync();
    }
}