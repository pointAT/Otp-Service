using Microsoft.EntityFrameworkCore;
using OtpService.Infrastructure.Persistence;
var builder = WebApplication.CreateBuilder(args);
var postgresConn = builder.Configuration.GetConnectionString("Postgres")
                   ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

builder.Services.AddDbContext<OtpDbContext>(options =>
{
    options.UseNpgsql(postgresConn, npgsql =>
    {
        
        npgsql.MigrationsAssembly(typeof(OtpDbContext).Assembly.GetName().Name);
        // Retry on transient connection failures (network blips, restarts)
        npgsql.EnableRetryOnFailure(maxRetryCount: 5);
    });
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging(false);  // explicitly OFF — OTPs leak otherwise
        options.EnableDetailedErrors();
    }
});
var app = builder.Build();

// ─── Auto-apply migrations on startup ─────────────────────────
// its for `docker compose up`.
// Migrations apply at startup; the app waits for Postgres healthcheck.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OtpDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();