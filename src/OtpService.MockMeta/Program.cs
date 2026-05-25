using System.Text.Json;
using OtpService.MockMeta;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MockMetaOptions>(
    builder.Configuration.GetSection("MockMeta"));

// HttpClient for posting webhooks
builder.Services.AddHttpClient<WebhookSender>();

var app = builder.Build();

// ─── Health endpoint ──────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// ─── POST /{phoneNumberId}/messages ───────────────────────
// Mimics the Meta Graph API "send message" endpoint.
// Returns a fake message id, then schedules a delivered webhook.
app.MapPost("/{phoneNumberId}/messages", async (
    string phoneNumberId,
    HttpRequest request,
    WebhookSender webhookSender,
    ILogger<Program> logger) =>
{
    // Parse the inbound payload just enough to grab the recipient msisdn
    using var doc = await JsonDocument.ParseAsync(request.Body);
    var msisdn = doc.RootElement.TryGetProperty("to", out var toEl)
        ? toEl.GetString() ?? "unknown"
        : "unknown";

    // Generate a fake Meta message id ("wamid.<random>")
    var messageId = $"wamid.{Guid.NewGuid():N}";

    logger.LogInformation(
        "Mock Meta accepted message for {Msisdn} on phone {PhoneNumberId} → {MessageId}",
        msisdn, phoneNumberId, messageId);

    // Schedule async webhook (fires ~1.5 seconds later)
    webhookSender.ScheduleDeliveredCallback(messageId, msisdn);

    // Respond with Meta-shaped success payload
    return Results.Ok(new
    {
        messaging_product = "whatsapp",
        contacts = new[] { new { input = msisdn, wa_id = msisdn.TrimStart('+') } },
        messages = new[] { new { id = messageId } }
    });
});

app.Run();