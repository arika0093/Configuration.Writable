using Configuration.Writable.Provider;
using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

// Add a writable configuration file located at the user config directory
// e.g. "%LOCALAPPDATA%/Configuration.Writable.Examples/user-settings.json" on Windows
builder
    .AddUserConfigurationFile<SampleSetting>(options =>
    {
        options.InstanceName = "worker-sample-1";
        options.FileName = "setting1.yaml";
        options.Provider = new WritableConfigYamlProvider<SampleSetting>();
    })
    .AddUserConfigurationFile<SampleSetting>(options =>
    {
        options.InstanceName = "worker-sample-2";
        options.FileName = "setting2.json";
        options.Provider = new WritableConfigJsonProvider<SampleSetting>();
    });

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
