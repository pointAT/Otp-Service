using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpService.Core.Contracts;
using OtpService.Providers.Abstractions;
using OtpService.Providers.Configuration;

//Mock implementation
namespace OtpService.Providers.Mock;

// Mock WhatsApp provider, Calls our Mock Meta container with the same payload
// shape that real Meta expects — so swapping to a real Meta implementation

public sealed class MockWhatsAppProvider : IWhatsAppProvider
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<MockWhatsAppProvider> _logger;

    public MockWhatsAppProvider(
        HttpClient http,
        IOptions<WhatsAppOptions> options,
        ILogger<MockWhatsAppProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProviderResult> SendAsync(SendOtpJob job, CancellationToken cancellationToken)
    {
        var url = $"/{_options.PhoneNumberId}/messages";

       
        var payload = new
        {
            messaging_product = "whatsapp",
            to = job.Msisdn,
            type = "template",
            template = new
            {
                name = "otp_" + job.Purpose.ToLowerInvariant(),
                language = new { code = "en" },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = new[]
                        {
                            new { type = "text", text = job.Code }
                        }
                    }
                }
            }
        };

        //  POST it 
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(url, payload, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Network error sending OTP for {TrackingId}", job.TrackingId);
            return new ProviderResult(
                Success: false,
                WhatsAppMessageId: null,
                Failure: ProviderFailureKind.Transient,
                ErrorMessage: ex.Message,
                StatusCode: null);
        }

        //  Classify the HTTP status 
        if (response.IsSuccessStatusCode)
        {
            // Parse Meta's response shape:
            //   { "messaging_product":"whatsapp", "messages":[{ "id":"wamid.HBg..." }] }
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            if (!doc.RootElement.TryGetProperty("messages", out var messages) ||
                messages.GetArrayLength() == 0 ||
                !messages[0].TryGetProperty("id", out var idElement) ||
                idElement.GetString() is not { } messageId)
            {
                return new ProviderResult(false, null, ProviderFailureKind.Permanent,
                    "Response did not contain a message id", (int)response.StatusCode);
            }


            
            

            return new ProviderResult(
                Success: true,
                WhatsAppMessageId: messageId,
                Failure: ProviderFailureKind.None,
                ErrorMessage: null,
                StatusCode: (int)response.StatusCode);
        }

        // Not success: classify by status code rules
        var failureKind = response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests          => ProviderFailureKind.RateLimited,
            >= HttpStatusCode.InternalServerError   => ProviderFailureKind.Transient,
            _                                       => ProviderFailureKind.Permanent
        };

        _logger.LogError("Meta API returned error status: {StatusCode} for tracking ID {TrackingId}", response.StatusCode, job.TrackingId);

        return new ProviderResult(
            Success: false,
            WhatsAppMessageId: null,
            Failure: failureKind,
            ErrorMessage: $"HTTP {(int)response.StatusCode}",
            StatusCode: (int)response.StatusCode);
    }
}