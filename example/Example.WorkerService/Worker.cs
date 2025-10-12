using Configuration.Writable;

namespace Example.WorkerService;

public class Worker(IWritableOptions<SampleSetting> options) : BackgroundService
{
    private bool IsRepeat = true;

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
}
