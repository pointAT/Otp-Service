using OtpService.Infrastructure.Configuration;
using OtpService.Infrastructure.Topology;

var builder = Host.CreateApplicationBuilder(args);

// Bind Kafka section from config (.env via Kafka__*)
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));

// Register the topic initializer — runs once at startup
builder.Services.AddHostedService<KafkaTopicInitializer>();


var host = builder.Build();
host.Run();