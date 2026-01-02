using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWritableOptions<SampleSetting>(conf =>
{
    // save file location is ./config/mysettings.json
    // extension is determined by the provider (omittable)
    conf.UseFile("./config/mysettings");

    // if you want to standard system configration location, use conf.UseStandardSaveDirectory("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    // conf.UseStandardSaveDirectory("your-app-id").AddFilePath("appdata-setting");

    // customize the provider and file writer
    // you can use Json, Xml, Yaml, Encrypted file, or your original format by implementing IFormatProvider
    conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);

    // if you want to keep backup files, use CommonFileProvider with BackupMaxCount > 0
    // conf.FileProvider = new CommonFileProvider() { BackupMaxCount = 5 };

    // if you want to validate the config before saving, use
    // * UseDataAnnotationsValidation: use data annotation attributes in your config class. Defaults to true.
    // * WithValidatorFunction: a simple way to set validation function
    // * WithValidator: set a custom validation class implementing IValidateOptions<T>
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
