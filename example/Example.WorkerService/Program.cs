using Configuration.Writable.Provider;
using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddUserConfigurationFile<SampleSetting>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
