namespace OtpService.Providers.Configuration;

public sealed class WhatsAppOptions
{
    ///mock or meta
    public string Provider { get; set; } = "mock";

    //Base URL of the provider. For mock = http://mockmeta:8080. For meta = https://graph.facebook.com
    public string BaseUrl { get; set; } = "http://mockmeta:8080";

    //Phone-number-id from Meta.  use a fake value in dev
    public string PhoneNumberId { get; set; } = "000000000000000";

    // Bearer token for Meta API auth. Mock ignores this; real Meta requires it
    public string AccessToken { get; set; } = "mock-token";

    //Meta API version, like v20.0. Mock ignores this
    public string ApiVersion { get; set; } = "v20.0";
}