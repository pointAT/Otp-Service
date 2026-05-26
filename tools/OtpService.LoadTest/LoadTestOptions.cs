namespace OtpService.LoadTest;

public sealed class LoadTestOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "otp.requests";

    //Target messages per second
    public int Rate { get; set; } = 1000;

  //Test duration in seconds
    public int Duration { get; set; } = 30;

    // "sustained" (constant rate), "burst" (immediate full rate),
    //or "mixed-priority" (sustained + a mix of P0/P1/P2 purposes)
    public string Mode { get; set; } = "sustained";

    public static LoadTestOptions ParseArgs(string[] args)
    {
        var opts = new LoadTestOptions();
        for (int i = 0; i < args.Length - 1; i += 2)
        {
            switch (args[i])
            {
                case "--bootstrap": opts.BootstrapServers = args[i + 1]; break;
                case "--topic":     opts.Topic = args[i + 1]; break;
                case "--rate":      opts.Rate = int.Parse(args[i + 1]); break;
                case "--duration":  opts.Duration = int.Parse(args[i + 1]); break;
                case "--mode":      opts.Mode = args[i + 1]; break;
            }
        }
        return opts;
    }
}