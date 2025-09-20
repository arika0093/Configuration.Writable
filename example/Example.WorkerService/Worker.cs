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
            Console.WriteLine($"Current Name is :: {options.CurrentValue.Name}");
            Console.Write($"Enter Name :: ");
            var name = Console.ReadLine();
            await options.SaveAsync(
                config =>
                {
                    config.Name = name ?? string.Empty;
                    config.LastUpdatedAt = DateTime.Now;
                },
                stoppingToken
            );
        }
    }
}
