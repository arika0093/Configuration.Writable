using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Example.WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddUserConfigurationFile<SampleSetting>(opt =>
{
    // save file location is ../config/mysettings.json
    // extension is determined by the provider (omittable)
    opt.FilePath = "../config/mysettings";

    // if you want to standard system configration location, use opt.UseStandardSaveLocation("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    //opt.UseStandardSaveLocation("your-app-id");

    // customize the provider and file writer
    // you can use Json, Xml, Yaml, Encrypted file, or your original format by implementing IWritableConfigProvider
    opt.Provider = new WritableConfigJsonProvider()
    {
        JsonSerializerOptions = { WriteIndented = true },
    };
    // if you want to keep backup files, use CommonFileWriter with BackupMaxCount > 0
    opt.FileWriter = new CommonFileWriter() { BackupMaxCount = 5 };
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
