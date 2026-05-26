using OtpService.Core.Abstractions;
using StackExchange.Redis;

namespace OtpService.Infrastructure.Redis;

// Redis-backed per-msisdn cooldown using SETNX with TTL.
// Mechanically identical to RedisDedupeStore — different key prefix

public sealed class RedisCooldownStore : ICooldownStore
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "cooldown:";

    public RedisCooldownStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> TryAcquireAsync(
        string msisdn,
        TimeSpan cooldown,
        CancellationToken cancellationToken)
    {
        var redisKey = KeyPrefix + msisdn;
        var db = _redis.GetDatabase();

        // When.NotExists makes this atomic. Two concurrent ingestion consumers
        // racing on the same msisdn will see only one return true.
        return await db.StringSetAsync(
            key: redisKey,
            value: "1",
            expiry: cooldown,
            when: When.NotExists);
    }
}