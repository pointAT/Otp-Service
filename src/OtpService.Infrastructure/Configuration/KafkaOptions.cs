namespace OtpService.Infrastructure.Configuration;

public sealed class KafkaOptions
{
    //bootstrap servers, topic name, partition count)
    public string BootstrapServers { get; set; } = default!;

    public string OtpRequestsTopic { get; set; } = default!;

    public int OtpRequestsPartitions { get; set; }

    public string ConsumerGroupId { get; set; } = default!;
    
    

    public string AutoOffsetReset { get; set; } = "Earliest";
}