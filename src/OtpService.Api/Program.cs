using Microsoft.EntityFrameworkCore;
using OtpService.Api.Endpoints;
using OtpService.Api.Webhooks;
using OtpService.Core.Configuration;
using OtpService.Core.Hashing;
using OtpService.Infrastructure.Persistence;
var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddSingleton<IOtpHasher, Sha256OtpHasher>();
builder.Services.AddSingleton<IOtpCodeGenerator, OtpCodeGenerator>();
var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OtpDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapStatusEndpoint();
app.MapVerifyEndpoint();
app.MapWhatsAppWebhookEndpoint();
app.Run();