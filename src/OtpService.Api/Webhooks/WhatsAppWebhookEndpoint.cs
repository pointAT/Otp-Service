using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OtpService.Core.Models;
using OtpService.Infrastructure.Persistence;

namespace OtpService.Api.Webhooks;

// Receives WhatsApp delivery-status callbacks and updates the matching OtpRecord.
public static class WhatsAppWebhookEndpoint
{
    public static IEndpointRouteBuilder MapWhatsAppWebhookEndpoint(this IEndpointRouteBuilder app)
    {
        
       
        app.MapGet("/webhooks/whatsapp", (
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.mode")] string? hubMode,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.verify_token")] string? hubVerifyToken,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "hub.challenge")] string? hubChallenge) =>
        {
            // TODO Day 6: verify hubVerifyToken == WhatsApp__VerifyToken from config.
            if (hubMode == "subscribe" && hubChallenge is not null)
                return Results.Text(hubChallenge);

            return Results.BadRequest();
        });

        // POST /webhooks/whatsapp — the real work
        // Receives delivery-status updates from Meta or Mock Meta.
        // Always returns 200 — even on bad input — so Meta doesn't retry
        app.MapPost("/webhooks/whatsapp", async (
            HttpRequest request,
            OtpDbContext db,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
          
            
            using var doc = await JsonDocument.ParseAsync(
                request.Body, cancellationToken: cancellationToken);

            
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
            logger.LogWarning(
                "Unknown webhook status '{Status}' for {MessageId}",
                statusString, messageId);
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