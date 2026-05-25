namespace OtpService.Core.Configuration;


public sealed class OtpOptions
{
    public int CodeLength { get; set; } = default!;
    public int TtlSeconds { get; set; } = default!;
    public int MaxVerifyAttempts { get; set; } = default!;
    public string Pepper { get; set; } = default!;
}