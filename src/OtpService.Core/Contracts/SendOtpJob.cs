namespace OtpService.Core.Contracts;

// Job published to RabbitMQ for the Delivery worker to send to WhatsApp.
public sealed record SendOtpJob(
    Guid TrackingId,     
    string Msisdn,
    string Code,         // plaintext
    string Purpose,
    int Attempt          // 1 on first send
);