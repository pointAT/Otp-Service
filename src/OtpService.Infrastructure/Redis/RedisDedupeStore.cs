using OtpService.Core.Abstractions;
using StackExchange.Redis;

namespace OtpService.Infrastructure.Redis;

/// (the StackExchange.Redis equivalent of the SETNX command) for atomic
/// "set only if not present" 

public sealed class RedisDedupeStore : IDedupeStore
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "dedupe:";

    public RedisDedupeStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> TryReserveAsync(
        string key,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        var redisKey = KeyPrefix + key;

        var db = _redis.GetDatabase();

     
        return await db.StringSetAsync(
            key: redisKey,
            value: "1",
            expiry: ttl,
            when: When.NotExists); // Redis-backed dedupe store. Uses StringSetAsync with When.NotExists

    }
}