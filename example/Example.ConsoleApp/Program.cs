using Configuration.Writable;
using Configuration.Writable.FileWriter;
using Example.ConsoleApp;

var options = WritableConfig.GetInstance<SampleSetting>();

// initialize the writable config system
// default save location is ./userconfig.json
options.Initialize();

// if you want to specify a custom save location, use the following instead:
options.Initialize(opt =>
{
    // save file location is ../config/mysettings.json
    // extension is determined by the provider (omittable)
    opt.FileName = "../config/mysettings";

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

// -------------------------------
// get the config instance
var sampleSetting = options.CurrentValue;
Console.WriteLine($">> Name: {sampleSetting.Name}, LastUpdatedAt: {sampleSetting.LastUpdatedAt}");

// save the config instance
Console.Write(":: Enter new name: ");
var newName = Console.ReadLine();
await options.SaveAsync(setting =>
{
    setting.Name = newName;
    setting.LastUpdatedAt = DateTime.Now;
});
Console.WriteLine(":: Config saved.");
Console.WriteLine($"   at {options.ConfigFilePath}");

// get updated config instance
var updatedSampleSetting = options.CurrentValue;
Console.WriteLine(
    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
);
