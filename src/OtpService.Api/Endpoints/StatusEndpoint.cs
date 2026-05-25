using Microsoft.EntityFrameworkCore;
using OtpService.Infrastructure.Persistence;

namespace OtpService.Api.Endpoints;



public static class StatusEndpoint
{
    public static IEndpointRouteBuilder MapStatusEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/otp/status/{trackingId:guid}", async (
            Guid trackingId,
            OtpDbContext db,
            CancellationToken cancellationToken) =>
        {
            // Read-only query → AsNoTracking skips EF's change tracker 
            var record = await db.OtpRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.TrackingId == trackingId, cancellationToken);

            if (record is null)
                return Results.NotFound();

            // Build a dto with only the safe fields
            return Results.Ok(new
            {
                trackingId = record.TrackingId,
                status = record.Status.ToString(),
                priority = record.Priority.ToString(),
                msisdn = MaskMsisdn(record.Msisdn),
                purpose = record.Purpose,
                createdAt = record.CreatedAt,
                expiresAt = record.ExpiresAt
            });
        });

        return app;
    }

    // Masks an E.164 msisdn for API responses
    // e.g. "+9647740605923" → "+9647***923"
    private static string MaskMsisdn(string msisdn)
    {
        if (string.IsNullOrEmpty(msisdn) || msisdn.Length <= 7)
            return msisdn;

        var prefix = msisdn[..4];
        var suffix = msisdn[^3..];
        return $"{prefix}***{suffix}";
    }
}