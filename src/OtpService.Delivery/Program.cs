using OtpService.Infrastructure.Configuration;
using OtpService.Infrastructure.Topology;

var builder = Host.CreateApplicationBuilder(args);
// Bind RabbitMq section from config (.env via RabbitMq__*)

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection("RabbitMq"));
// Register the topic initializer — runs once at startup

builder.Services.AddHostedService<RabbitMqTopologyInitializer>();


var host = builder.Build();
host.Run();