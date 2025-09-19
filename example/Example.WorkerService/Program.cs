using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

// Add a writable configuration file located at "./settings/user-settings.json"
//builder.AddUserConfigurationFile<SampleSetting>("./settings/user-settings.json");

// Add a writable configuration file located at the user config directory
// e.g. "%LOCALAPPDATA%/Configuration.Writable.Examples/user-settings.json" on Windows
builder.AddUserConfigurationToAppConfigFolder<SampleSetting>(
    "user-settings.json",
    "Configuration.Writable.Examples"
);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
