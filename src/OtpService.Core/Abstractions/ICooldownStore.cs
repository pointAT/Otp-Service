namespace OtpService.Core.Abstractions;

// Per-msisdn cooldown to prevent OTP-spam attacks.
//   - Dedupe = "have we processed THIS request already?" (idempotency)
//   - Cooldown = "is this PHONE NUMBER throttled?" (abuse prevention)

public interface ICooldownStore
{
    //   true  → no recent OTP for this number, you may proceed (lock set with TTL)
    //   false → number is in cooldown, REJECT this request
    Task<bool> TryAcquireAsync(string msisdn, TimeSpan cooldown, CancellationToken cancellationToken);
}