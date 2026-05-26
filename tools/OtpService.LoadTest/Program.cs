using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using OtpService.LoadTest;

var options = LoadTestOptions.ParseArgs(args);

Console.WriteLine($"=== OTP Load Test ===");
Console.WriteLine($"Bootstrap:  {options.BootstrapServers}");
Console.WriteLine($"Topic:      {options.Topic}");
Console.WriteLine($"Mode:       {options.Mode}");
Console.WriteLine($"Rate:       {options.Rate} msg/sec");
Console.WriteLine($"Duration:   {options.Duration}s");
Console.WriteLine($"Total:      {options.Rate * options.Duration:N0} messages");
Console.WriteLine();


var producerConfig = new ProducerConfig
{
    BootstrapServers = options.BootstrapServers,
    ClientId = "otp-load-test",
    // Batch up to 16KB or 5ms, whichever comes first — improves throughput at cost of small latency
    BatchSize = 16384,
    LingerMs = 5,
    // Compression saves bandwidth + disk space on Kafka side
    CompressionType = CompressionType.Snappy,
    // Strong durability — broker must ack before we count it as sent
    Acks = Acks.All,
    // Allow buffer to hold many in-flight messages
    QueueBufferingMaxMessages = 1_000_000
};

using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

// Stats
long produced = 0;
long failed = 0;
var stopwatch = Stopwatch.StartNew();

// Pre-build a pool of msisdns so we have variety in partition keys
var random = new Random(42);
var msisdns = Enumerable.Range(0, 100_000)
    .Select(_ => $"+9647{random.Next(10000000, 99999999)}")
    .ToArray();

// Purpose pool for mixed-priority mode (probability-weighted)
// Distribution: 10% P0 (txn_signing), 70% P1 (login), 20% P2 (bulk_notify)
string PickPurpose()
{
    var roll = random.Next(100);
    if (roll < 10) return "txn_signing";
    if (roll < 80) return "login";
    return "bulk_notify";
}

Message<string, string> BuildMessage(int seq)
{
    var msisdn = msisdns[random.Next(msisdns.Length)];
    var purpose = options.Mode == "mixed-priority" ? PickPurpose() : "login";

    var payload = new
    {
        RequestId = $"load-{Guid.NewGuid():N}",
        Msisdn = msisdn,
        Purpose = purpose,
        Channel = "whatsapp",
        Priority = (int?)null
    };

    return new Message<string, string>
    {
        Key = msisdn,                                          
        Value = JsonSerializer.Serialize(payload)
    };
}

// Delivery callback — count successes and failures asynchronously
void OnDelivery(DeliveryReport<string, string> report)
{
    if (report.Error.IsError)
        Interlocked.Increment(ref failed);
    else
        Interlocked.Increment(ref produced);
}

int totalMessages = options.Rate * options.Duration;
int sentSoFar = 0;

if (options.Mode == "burst")
{
    // Burst mode: produce as fast as possible, no rate limiting.
    // The whole batch goes into the producer's internal buffer immediately;
    // it'll flush to Kafka at whatever the system allows.
    Console.WriteLine($"BURST: producing {totalMessages:N0} messages as fast as possible...");
    for (int i = 0; i < totalMessages; i++)
    {
        producer.Produce(options.Topic, BuildMessage(i), OnDelivery);
        sentSoFar++;
    }
}
else
{
    // Sustained / mixed-priority mode: pace the producer to hit the target rate.
    // We send in 100ms chunks: rate/10 messages every 100ms.
    int messagesPerTick = Math.Max(1, options.Rate / 10);
    var tickInterval = TimeSpan.FromMilliseconds(100);

    Console.WriteLine($"SUSTAINED: ~{messagesPerTick} msgs every 100ms for {options.Duration}s...");
    var endTime = DateTime.UtcNow.AddSeconds(options.Duration);
    var nextTick = DateTime.UtcNow;

    while (DateTime.UtcNow < endTime)
    {
        for (int i = 0; i < messagesPerTick; i++)
        {
            producer.Produce(options.Topic, BuildMessage(sentSoFar), OnDelivery);
            sentSoFar++;
        }

        // Print progress every second
        if ((int)stopwatch.Elapsed.TotalSeconds != (int)(stopwatch.Elapsed.TotalSeconds - 0.1))
        {
            Console.Write($"\rElapsed: {stopwatch.Elapsed.TotalSeconds:F1}s | " +
                          $"Sent: {sentSoFar:N0} | Acked: {produced:N0} | Failed: {failed:N0}    ");
        }

        nextTick += tickInterval;
        var wait = nextTick - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait);
    }
}

Console.WriteLine();
Console.WriteLine($"All {sentSoFar:N0} messages queued. Flushing producer...");

producer.Flush(TimeSpan.FromSeconds(30));
stopwatch.Stop();

// Final report 
Console.WriteLine();
Console.WriteLine($"=== Results ===");
Console.WriteLine($"Mode:              {options.Mode}");
Console.WriteLine($"Duration (wall):   {stopwatch.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"Messages queued:   {sentSoFar:N0}");
Console.WriteLine($"Messages acked:    {produced:N0}");
Console.WriteLine($"Messages failed:   {failed:N0}");
Console.WriteLine($"Effective rate:    {produced / stopwatch.Elapsed.TotalSeconds:F0} msg/sec");
Console.WriteLine();