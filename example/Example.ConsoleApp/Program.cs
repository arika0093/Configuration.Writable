using Configuration.Writable;
using Configuration.Writable.FileProvider;
using Example.ConsoleApp;
using Microsoft.Extensions.Logging;

// initialize the writable config system
// default save location is ./userconfig.json
WritableConfig.Initialize<SampleSetting>();

// if you want to specify a custom save location, use the following instead:
WritableConfig.Initialize<SampleSetting>(opt =>
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
        // if you want to keep backup files, use CommonFileProvider with BackupMaxCount > 0
        // FileProvider = new CommonFileProvider() { BackupMaxCount = 5 };
        JsonSerializerOptions = { WriteIndented = true },
    };

    // if you want to use logging, set Logger
    // required NuGet package: Microsoft.Extensions.Logging.Console
    // opt.Logger = LoggerFactory
    //    .Create(builder => builder.AddConsole())
    //    .CreateLogger("UserConfig");

    // if you want to validate the config before saving, use
    // * UseDataAnnotationsValidation: use data annotation attributes in your config class. Defaults to true.
    // * WithValidatorFunction: a simple way to set validation function
    // * WithValidator: set a custom validation class implementing IValidateOptions<T>
    //
    // If use DataAnnotation validation with Source Generators,
    // see SampleSettingValidator class in this project and comment out below code.
    opt.UseDataAnnotationsValidation = false;
    opt.WithValidator<SampleSettingValidator>();
});

// -------------------------------
// get the config instance
var options = WritableConfig.GetOptions<SampleSetting>();

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
Console.WriteLine($"   at {options.GetConfigurationOptions().ConfigFilePath}");

// get updated config instance
var updatedSampleSetting = options.CurrentValue;
Console.WriteLine(
    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
);
