using System.Security.Cryptography;
using System.Text;

namespace OtpService.Api.Webhooks;

// Verifies Meta's X-Hub-Signature-256 HMAC against the raw request body.
// Uses constant-time comparison to prevent timing attacks where an attacker
// learns the signature byte-by-byte by measuring response times.
public static class HmacVerifier
{
  
    public static bool Verify(byte[] rawBody, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        // Meta's format is "sha256=<hexhash>". Strip the prefix if present.
        const string prefix = "sha256=";
        var receivedHex = signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? signatureHeader.Substring(prefix.Length)
            : signatureHeader;

        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        var expectedHash = HMACSHA256.HashData(keyBytes, rawBody);
        var expectedHex = Convert.ToHexString(expectedHash).ToLowerInvariant();

       
        var receivedBytes = Encoding.UTF8.GetBytes(receivedHex.ToLowerInvariant());
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex);

        return CryptographicOperations.FixedTimeEquals(receivedBytes, expectedBytes);
    }
}