using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OtpService.Core.Configuration;

namespace OtpService.Core.Hashing;

public sealed class Sha256OtpHasher : IOtpHasher
{
    private readonly byte[] _pepperBytes;

    public Sha256OtpHasher(IOptions<OtpOptions> options)
    {
        _pepperBytes = Encoding.UTF8.GetBytes(options.Value.Pepper);
    }

    public byte[] Hash(string code, byte[] salt)
    {
        byte[] codeBytes = Encoding.UTF8.GetBytes(code);

        //  salt + code + pepper
        byte[] combined = new byte[salt.Length + codeBytes.Length + _pepperBytes.Length];
        
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(codeBytes, 0, combined, salt.Length, codeBytes.Length);
        Buffer.BlockCopy(_pepperBytes, 0, combined, salt.Length + codeBytes.Length, _pepperBytes.Length);

        return SHA256.HashData(combined);
    }

    public byte[] GenerateSalt()
    {
        // Return  secure 32-byte salt
        return RandomNumberGenerator.GetBytes(32);
    }
    
    public bool FixedTimeEquals(byte[] a, byte[] b)
    {
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

   
}