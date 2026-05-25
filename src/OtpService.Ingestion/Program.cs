using Microsoft.EntityFrameworkCore;
using OtpService.Core.Configuration;
using OtpService.Core.Hashing;
using OtpService.Infrastructure.Configuration;
using OtpService.Infrastructure.Persistence;
using OtpService.Infrastructure.RabbitMq;
using OtpService.Infrastructure.Topology;
using OtpService.Ingestion.Consumers;

var builder = Host.CreateApplicationBuilder(args);

// Bind Kafka section from config (.env via Kafka__*)
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
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

//Hasing service
builder.Services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
builder.Services.AddSingleton<IOtpHasher, Sha256OtpHasher>();


// Registered as singleton first so both the consumer worker AND the hosted-service
// framework get the SAME instance.
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddSingleton<IRabbitMqPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqPublisher>());

// Register the topic initializer — runs once at startup
builder.Services.AddHostedService<KafkaTopicInitializer>();
builder.Services.AddHostedService<OtpRequestsConsumer>();


var host = builder.Build();
host.Run();