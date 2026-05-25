namespace OtpService.Infrastructure.Configuration;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = default!;
    public int Port { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string VirtualHost { get; set; } = default!;


    



}