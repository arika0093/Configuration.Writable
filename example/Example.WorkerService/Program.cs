using Configuration.Writable;
using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddUserConfigurationFile<SampleSetting>(options =>
{
    options.Provider = WritableConfigProvider.Json;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
