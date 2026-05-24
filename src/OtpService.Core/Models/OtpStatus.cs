namespace OtpService.Core.Models;

public enum OtpStatus
{
    Queued = 0,
    Dispatched = 1,
    Sent = 2,
    Delivered = 3,
    Verified = 4,
    Expired = 5,
    Locked = 6,
    Failed = 7,
    DeadLettered = 8
}