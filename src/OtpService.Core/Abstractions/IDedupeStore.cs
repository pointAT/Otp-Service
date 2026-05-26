namespace OtpService.Core.Abstractions;

// Atomic "have we seen this key before?" check used to dedupe at-least-once
// message delivery. Backed by Redis SETNX with TTL.
public interface IDedupeStore
{
    
    // Try to reserve a key for processing. Returns:
    //   true  → key was new, you have permission to do the work
    //   false → key already exists, this is a duplicate, skip
   
    Task<bool> TryReserveAsync(string key, TimeSpan ttl, CancellationToken cancellationToken);
}