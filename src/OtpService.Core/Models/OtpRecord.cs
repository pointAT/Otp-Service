using System;

namespace OtpService.Core.Models
{
    public sealed class OtpRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TrackingId { get; set; } = Guid.NewGuid();

        // Producer-supplied idempotency key — unique constraint enforced at DB level
        public string RequestId { get; set; } = default!;

        public string Msisdn { get; set; } = default!;

        // Reason: login, txn_signing
        public string Purpose { get; set; } = default!;

        
        public OtpPriority Priority { get; set; }

        // SHA-256(salt || code || pepper)
        public byte[] CodeHash { get; set; } = default!;

        // Per-OTP random 32-byte salt
        public byte[] Salt { get; set; } = default!;

        public int Attempts { get; set; } = 0;
        public int MaxAttempts { get; set; } = 5;

        public OtpStatus Status { get; set; } = OtpStatus.Queued;

        public string? WhatsAppMessageId { get; set; }

        // Last webhook timestamp (unix ms from Meta). Used to ignore out-of-order callbacks
        public long? LastAppliedTimestamp { get; set; }

        public string? FailureReason { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }

        //Optimistic concurrency ،ean that he check if race condition happend or not 
        public uint Xmin { get; set; }
    }
}