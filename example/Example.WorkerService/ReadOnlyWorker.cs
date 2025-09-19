using Configuration.Writable;

namespace Example.WorkerService;

public class ReadOnlyWorker(IReadonlyOptions<SampleSetting> options) : BackgroundService
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(10));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000);
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine($"Current Name is :: {options.CurrentValue.Name}");
            await _timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
