namespace OtpService.Core.Models;

// Maps an OTP purpose string to a priority 


public static class PriorityMapper
{
    // Get the system-assigned priority for a purpose.
    public static OtpPriority FromPurpose(string purpose)
    {
        // Normalize so "Login", "LOGIN", "login" all map the same.
        var normalized = purpose?.ToLowerInvariant() ?? string.Empty;

        return normalized switch
        {
            // High 
            "txn_signing"      => OtpPriority.High,
            "payment_confirm"  => OtpPriority.High,
            "account_recovery" => OtpPriority.High,

            // Low — best-effort, can be delayed under load
            "bulk_notify"      => OtpPriority.Low,
            "promo_verify"     => OtpPriority.Low,

            // Everything else (including unknown) → Normal
            _                  => OtpPriority.Normal
        };
    }

   
    // Apply the producer's optional downgrade.
    // If the producer supplied a lower priority than the system would assign,
    // honor it. If they tried to upgrade, ignore.
    //the mean by ""honor"" ,is when the producer ask for this request to be a high priorty
    //(but its lower than its ,so it will igonre it)
    public static OtpPriority ApplyProducerDowngrade(
        OtpPriority systemAssigned,
        int? producerSupplied)
    {
        if (producerSupplied is null)
            return systemAssigned;

        // Convert int → enum if it's a valid value 
        if (producerSupplied != (int)OtpPriority.Low &&
            producerSupplied != (int)OtpPriority.Normal &&
            producerSupplied != (int)OtpPriority.High)
        {
            return systemAssigned;   
        }

        var requested = (OtpPriority)producerSupplied;
        // Only allow lowering, never raising
        return requested < systemAssigned ? requested : systemAssigned;
    }
}