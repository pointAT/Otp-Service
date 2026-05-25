namespace OtpService.MockMeta;

public sealed class MockMetaOptions
{
    // Where to POST the webhook callback 
    
    public string WebhookUrl { get; set; } = default!;

    
    // Shared secret used to sign the webhook with HMAC-SHA256
    public string AppSecret { get; set; } = default!;

    
    // Delay before firing the delivery webhook
    public int WebhookDelayMs { get; set; } = 1500;
}