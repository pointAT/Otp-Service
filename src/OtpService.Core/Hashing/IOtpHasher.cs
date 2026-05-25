namespace OtpService.Core.Hashing;

public interface IOtpHasher
{
    //Hash a plaintext code with a per-OTP salt +  pepper
    byte[] Hash(string code, byte[] salt);

    //Generate 32-byte random salt.
    byte[] GenerateSalt();

    
}