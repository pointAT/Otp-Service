using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpService.Core.Models;
using OtpService.Infrastructure.Persistence;
using OtpService.Providers.Configuration;

namespace OtpService.Api.Webhooks;

public static class WhatsAppWebhookEndpoint
{
    public static IEndpointRouteBuilder MapWhatsAppWebhookEndpoint(this IEndpointRouteBuilder app)
    {
      
        app.MapGet("/webhooks/whatsapp", (
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.mode")] string? hubMode,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.verify_token")] string? hubVerifyToken,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.challenge")] string? hubChallenge,
            IOptions<WhatsAppOptions> waOptions,
            ILogger<Program> logger) =>
        {
            if (hubMode != "subscribe" || hubChallenge is null)
                return Results.BadRequest();

            // Verify the token matches our configured one. Constant-time compare
            // to prevent timing attacks, same principle as the HMAC verifier.
            var expected = System.Text.Encoding.UTF8.GetBytes(waOptions.Value.VerifyToken);
            var received = System.Text.Encoding.UTF8.GetBytes(hubVerifyToken ?? string.Empty);

            if (received.Length != expected.Length ||
                !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(received, expected))
            {
                logger.LogWarning("Webhook subscription denied — invalid verify token");
                return Results.Forbid();
            }

            return Results.Text(hubChallenge);
        });

        // POST /webhooks/whatsapp — the real work
        // Receives delivery-status updates from Meta or Mock Meta.
        // Always returns 200 — even on bad input — so Meta doesn't retry
        app.MapPost("/webhooks/whatsapp", async (
            HttpRequest request,
            OtpDbContext db,
            IOptions<WhatsAppOptions> waOptions,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            // We MUST read the body before any parsing. The HMAC is computed
            // over the raw bytes — reformatting (whitespace, key reordering)
            // breaks the signature.
            byte[] rawBody;
            using (var ms = new MemoryStream())
            {
                await request.Body.CopyToAsync(ms, cancellationToken);
                rawBody = ms.ToArray();
            }

            if (rawBody.Length == 0)
            {
                logger.LogWarning("Webhook with empty body — rejecting");
                return Results.BadRequest();
            }

            //  Verify the HMAC signature 
            var signatureHeader = request.Headers["X-Hub-Signature-256"].FirstOrDefault();

            if (!HmacVerifier.Verify(rawBody, signatureHeader, waOptions.Value.AppSecret))
            {
                logger.LogWarning(
                    "Webhook signature verification FAILED (header present: {HasHeader})",
                    signatureHeader is not null);
                
                // 401 Unauthorized — Meta interprets this as "stop sending me here"
              
                return Results.Unauthorized();
            }

            //  Parse the JSON (now safe ) 
            using var doc = JsonDocument.Parse(rawBody);

            if (!doc.RootElement.TryGetProperty("entry", out var entry) ||
                entry.GetArrayLength() == 0)
            {
                logger.LogWarning("Webhook payload missing 'entry'");
                return Results.Ok();
            }

            if (!entry[0].TryGetProperty("changes", out var changes) ||
                changes.GetArrayLength() == 0)
            {
                logger.LogWarning("Webhook payload missing 'changes'");
                return Results.Ok();
            }

            if (!changes[0].TryGetProperty("value", out var value) ||
                !value.TryGetProperty("statuses", out var statuses) ||
                statuses.GetArrayLength() == 0)
            {
                logger.LogWarning("Webhook payload missing 'statuses'");
                return Results.Ok();
            }

            foreach (var status in statuses.EnumerateArray())
            {
                if (!status.TryGetProperty("id", out var idEl) ||
                    !status.TryGetProperty("status", out var statusEl))
                    continue;

                var messageId = idEl.GetString();
                var statusString = statusEl.GetString();
                if (messageId is null || statusString is null) continue;

                await ApplyStatusUpdateAsync(db, messageId, statusString, logger, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        });

        return app;
    }

    private static async Task ApplyStatusUpdateAsync(
        OtpDbContext db,
        string messageId,
        string statusString,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var record = await db.OtpRecords
            .FirstOrDefaultAsync(r => r.WhatsAppMessageId == messageId, cancellationToken);

        if (record is null)
        {
            logger.LogWarning("Webhook for unknown message id {MessageId}", messageId);
            return;
        }

        OtpStatus? newStatus = statusString.ToLowerInvariant() switch
        {
            "sent"      => OtpStatus.Sent,
            "delivered" => OtpStatus.Delivered,
            "read"      => OtpStatus.Delivered,
            "failed"    => OtpStatus.Failed,
            _           => null
        };

        if (newStatus is null)
        {
            logger.LogWarning("Unknown webhook status '{Status}' for {MessageId}", statusString, messageId);
            return;
        }

        if (record.Status == OtpStatus.Verified ||
            record.Status == OtpStatus.Locked   ||
            record.Status == OtpStatus.Expired)
        {
            return;
        }

        record.Status = newStatus.Value;

        logger.LogInformation(
            "Webhook applied: {MessageId} → {Status}",
            messageId, newStatus.Value);
    }
}