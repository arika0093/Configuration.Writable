using Example.SimpleCase;

var builder = Host.CreateApplicationBuilder(args);

//builder.AddUserConfigurationFile<SampleSetting>("./settings/user-settings.json");
builder.AddUserConfigurationToAppConfigFolder<SampleSetting>(
    "user-settings.json",
    "Configuration.Writable.Examples"
);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
