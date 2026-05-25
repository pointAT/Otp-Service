namespace OtpService.Core.Contracts;

// Shape of messages on the otp.requests Kafka topic.
public sealed record OtpRequestMessage(
    string RequestId,    // producer's idempotency key
    string Msisdn,       
    string Purpose,      // "login", "txn_signing"
    string Channel,      // "whatsapp"
    int? Priority
);