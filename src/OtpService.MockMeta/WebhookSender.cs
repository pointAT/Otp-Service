using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OtpService.MockMeta;


public sealed class WebhookSender
{
    private readonly HttpClient _http;
    private readonly MockMetaOptions _options;
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(
        HttpClient http,
        IOptions<MockMetaOptions> options,
        ILogger<WebhookSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    // Schedule a "delivered" webhook for the given message id.
    // Fires after the configured delay; does not block the caller.
    public void ScheduleDeliveredCallback(string messageId, string msisdn)
    {
        // Fire-and-forget. We deliberately don't await this — the HTTP response
        // to the original /messages call has already been returned to the worker.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_options.WebhookDelayMs);
                await SendWebhookAsync(messageId, msisdn, status: "delivered");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send delivered webhook for {MessageId}", messageId);
            }
        });
    }

    private async Task SendWebhookAsync(string messageId, string msisdn, string status)
    {
        // Build the Meta-shaped payload
        var payload = new
        {
            entry = new[]
            {
                new
                {
                    changes = new[]
                    {
                        new
                        {
                            value = new
                            {
                                statuses = new[]
                                {
                                    new
                                    {
                                        id = messageId,
                                        status = status,
                                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                                        recipient_id = msisdn.TrimStart('+')
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bodyBytes = Encoding.UTF8.GetBytes(json);

        // Sign with HMAC-SHA256 using the shared app secret
        var signature = ComputeHmac(bodyBytes, _options.AppSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.WebhookUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");

        var response = await _http.SendAsync(request);

        _logger.LogInformation(
            "Sent {Status} webhook for {MessageId} → {StatusCode}",
            status, messageId, (int)response.StatusCode);
    }

    private static string ComputeHmac(byte[] body, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, body);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}