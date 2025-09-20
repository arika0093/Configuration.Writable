using Configuration.Writable;

namespace Example.WorkerService;

public class Worker(IWritableOptions<SampleSetting> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000);
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("-------------------------");
            Console.WriteLine($"Current Name 1 is :: {options.Get("worker-sample-1").Name}");
            Console.WriteLine($"Current Name 2 is :: {options.Get("worker-sample-2").Name}");
            Console.Write($"Enter Name 1 :: ");
            var name = Console.ReadLine();
            await options.SaveAsync(
                config =>
                {
                    config.Name = name ?? string.Empty;
                    config.LastUpdatedAt = DateTime.Now;
                },
                "worker-sample-1",
                stoppingToken
            );
            Console.Write($"Enter Name 2 :: ");
            var name2 = Console.ReadLine();
            await options.SaveAsync(
                config =>
                {
                    config.Name = name2 ?? string.Empty;
                    config.LastUpdatedAt = DateTime.Now;
                },
                "worker-sample-2",
                stoppingToken
            );
        }
    }
}
