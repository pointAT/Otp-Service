using Microsoft.EntityFrameworkCore;
using OtpService.Core.Models;

namespace OtpService.Infrastructure.Persistence;


public sealed class OtpDbContext(DbContextOptions<OtpDbContext> options) : DbContext(options)
{
    public DbSet<OtpRecord> OtpRecords { get; set; } 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OtpDbContext).Assembly);
    }
}