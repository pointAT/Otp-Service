using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OtpService.Core.Configuration;

namespace OtpService.Core.Hashing;

public sealed class OtpCodeGenerator : IOtpCodeGenerator
{
    private readonly OtpOptions _options;

    public OtpCodeGenerator(IOptions<OtpOptions> options)
    {
        _options = options.Value;
    }

    public string Generate()
    {
        // 1. Calculate max value: 10^codeLength (e.g. 1_000_000 for length 6)
        int maxValue = (int)Math.Pow(10, _options.CodeLength);

        // 2. Use RandomNumberGenerator.GetInt32(0, max) to get a random int
        int randomNumber = RandomNumberGenerator.GetInt32(0, maxValue);

        // 3. Format with leading zeros: number.ToString($"D{_options.CodeLength}")
        return randomNumber.ToString($"D{_options.CodeLength}");
    }
}