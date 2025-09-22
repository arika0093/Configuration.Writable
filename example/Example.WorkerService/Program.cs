using Configuration.Writable;
using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder
    .AddUserConfigurationFile<SampleSetting>(options =>
    {
        options.InstanceName = "worker-sample-1";
        options.Provider = new WritableConfigJsonProvider();
        options.FileName = "config/root-setting.json";
    })
    .AddUserConfigurationFile<SampleSetting>(options =>
    {
        options.InstanceName = "worker-sample-2";
        options.Provider = new WritableConfigJsonProvider();
        options.FileName = "config/child-setting.json";
    });

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
