using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWritableOptions(services =>
{
    // shared configuration for all options types
    services.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);

    // One backup is kept by default. Set a custom count when needed.
    // services.FileProvider = new CommonFileProvider() { BackupMaxCount = 5 };

    // if you want to use logging, set Logger
    // services.Logger = LoggerFactory
    //    .Create(builder => builder.AddZLoggerConsole())
    //    .CreateLogger("UserConfig");

    // if you want to standard system configuration location, use services.UseStandardSaveDirectory("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    // services.UseStandardSaveDirectory("your-app-id");

    // add SampleSetting
    services.Add<SampleSetting>(c =>
    {
        // save file location is ./config/mysettings.json
        // extension is determined by the provider (omittable)
        c.UseFile("./config/mysettings");

        // if you want to validate the config before saving, use
        // * UseDataAnnotationsValidation: use data annotation attributes in your config class. Defaults to true.
        // * WithValidatorFunction: a simple way to set validation function
        // * WithValidator: set a custom validation class implementing IValidateOptions<T>
    });
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
