using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

// Add a writable configuration file located at the user config directory
// e.g. "%LOCALAPPDATA%/Configuration.Writable.Examples/user-settings.json" on Windows
builder.AddUserConfigurationFile<SampleSetting>();

builder.Services.AddHostedService<ReadOnlyWorker>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
