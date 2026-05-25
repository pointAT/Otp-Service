namespace OtpService.Providers.Abstractions;

//return value (success/failure record)

public sealed record ProviderResult(
    bool Success,
    string? WhatsAppMessageId,      // e.g. "wamid.HBgM..." — set on success only
    ProviderFailureKind Failure,    // None on success
    string? ErrorMessage,           
    int? StatusCode                 
);

// Classifies why a send failed.
//- Transient → retry with exponential backoff
//- Permanent → dead-letter immediately, don't waste 
//- RateLimited → retry, 

public enum ProviderFailureKind
{
    None = 0,
    Transient = 1,
    RateLimited = 2,
    Permanent = 3
}
