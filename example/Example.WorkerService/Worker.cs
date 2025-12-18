using Configuration.Writable;

namespace Example.WorkerService;

public class Worker(IWritableOptions<SampleSetting> options) : BackgroundService
{
    private bool IsRepeat = true;
    private IDisposable? _changeListener;

    public override Task StartAsync(CancellationToken stoppingToken)
    {
        // register change listener
        _changeListener = options.OnChange((setting, _) => {
            Console.WriteLine("## Config changed notification received.");
            Console.WriteLine($"   New Name: {setting.Name}, LastUpdatedAt: {setting.LastUpdatedAt}");
        });

        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            // get the config instance
            var sampleSetting = options.CurrentValue;
            Console.WriteLine(
                $">> Name: {sampleSetting.Name}, LastUpdatedAt: {sampleSetting.LastUpdatedAt}"
            );

            // save the config instance
            Console.Write(":: Enter new name: ");
            var newName = Console.ReadLine();
            await options.SaveAsync(
                setting =>
                {
                    setting.Name = newName;
                    setting.LastUpdatedAt = DateTime.Now;
                },
                stoppingToken
            );

            if (!IsRepeat)
            {
                // get updated config instance
                var updatedSampleSetting = options.CurrentValue;
                Console.WriteLine(
                    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
                );
                // finish
                Environment.Exit(0);
            }
        }
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
        _changeListener?.Dispose();
        return base.StopAsync(stoppingToken);
    }
}
