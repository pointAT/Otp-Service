using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OtpService.Infrastructure.Persistence
{
    public class OtpDbContextFactory : IDesignTimeDbContextFactory<OtpDbContext>
    {
        public OtpDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<OtpDbContext>();

            // استخدم نفس اسم Connection String الموجود في appsettings.json
            // أو قم بقراءته من متغير بيئة إذا أردت
            var connectionString =
                Environment.GetEnvironmentVariable("Postgres")
                ?? "Host=187.124.171.6;Port=5432;Database=postgres;Username=postgres;Password=73BZk0dtk5SfegxZANCYGBTB149pTjRx9zyLPJleACmYuqGdtwYj3XxLz1JEVBbU;";

            optionsBuilder.UseNpgsql(connectionString);

            return new OtpDbContext(optionsBuilder.Options);
        }
    }
}