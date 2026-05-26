using Microsoft.EntityFrameworkCore;
using OtpService.Core.Configuration;
using OtpService.Delivery.Consumers;
using OtpService.Infrastructure.Configuration;
using OtpService.Infrastructure.Persistence;
using OtpService.Infrastructure.RabbitMq;
using OtpService.Infrastructure.Topology;
using OtpService.Providers.Abstractions;
using OtpService.Providers.Configuration;
using OtpService.Providers.Mock;

var builder = Host.CreateApplicationBuilder(args);
// Bind RabbitMq section from config (.env via RabbitMq__*)

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<OtpOptions>(
    builder.Configuration.GetSection("Otp"));

var postgresConn = builder.Configuration.GetConnectionString("Postgres")
                   ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddDbContext<OtpDbContext>(options =>
{
    options.UseNpgsql(postgresConn, npgsql =>
    {
        npgsql.MigrationsAssembly(typeof(OtpDbContext).Assembly.GetName().Name);
        npgsql.EnableRetryOnFailure(maxRetryCount: 5);
    });
});

builder.Services.AddHttpClient<IWhatsAppProvider, MockWhatsAppProvider>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WhatsAppOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
// Register the topic initializer — runs once at startup

builder.Services.AddHostedService<RabbitMqTopologyInitializer>();
builder.Services.AddHostedService<OtpDeliveryConsumer>();
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IRabbitMqPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqPublisher>());



var host = builder.Build();
host.Run();