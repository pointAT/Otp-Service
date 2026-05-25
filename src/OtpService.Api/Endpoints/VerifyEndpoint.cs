using Microsoft.EntityFrameworkCore;
using OtpService.Core.Hashing;
using OtpService.Core.Models;
using OtpService.Infrastructure.Persistence;

namespace OtpService.Api.Endpoints;


public sealed record VerifyRequest(Guid TrackingId, string Code);


public static class VerifyEndpoint
{
    public static IEndpointRouteBuilder MapVerifyEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/otp/verify", async (
            VerifyRequest request,
            OtpDbContext db,
            IOtpHasher hasher,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            //  Basic input validation 
            if (string.IsNullOrWhiteSpace(request.Code))
                return Results.BadRequest(new { error = "code is required" });

            
            var record = await db.OtpRecords
                .FirstOrDefaultAsync(r => r.TrackingId == request.TrackingId, cancellationToken);

            if (record is null)
                return Results.NotFound(new { error = "not_found" });

          
            if (record.Status == OtpStatus.Locked)
                return Results.Ok(new { status = "locked" });

            if (record.Status == OtpStatus.Verified)
                return Results.Ok(new { status = "verified" });

            if (record.Status == OtpStatus.Failed || record.Status == OtpStatus.DeadLettered)
                return Results.Ok(new { status = "invalid" });

           
            if (DateTimeOffset.UtcNow > record.ExpiresAt)
            {
                record.Status = OtpStatus.Expired;
                await db.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { status = "expired" });
            }

            var submittedHash = hasher.Hash(request.Code, record.Salt);

            if (hasher.FixedTimeEquals(record.CodeHash, submittedHash))
            {
                record.Status = OtpStatus.Verified;
                record.VerifiedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "OTP verified for {TrackingId}",
                    record.TrackingId);

                return Results.Ok(new { status = "verified" });
            }

            //  Wrong code — increment attempts, maybe lock 
            record.Attempts++;

            if (record.Attempts >= record.MaxAttempts)
            {
                record.Status = OtpStatus.Locked;
                await db.SaveChangesAsync(cancellationToken);

                logger.LogWarning(
                    "OTP locked after {Attempts} failed attempts for {TrackingId}",
                    record.Attempts, record.TrackingId);

                return Results.Ok(new { status = "locked" });
            }

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                status = "invalid",
                remainingAttempts = record.MaxAttempts - record.Attempts
            });
        });

        return app;
    }
}