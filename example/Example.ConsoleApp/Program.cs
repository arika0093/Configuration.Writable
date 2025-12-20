using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.ConsoleApp;

// initialize the writable config system
// default save location is ./userconfig.json
WritableOptions.Initialize<SampleSetting>();

// if you want to specify a custom save location, use the following instead:
WritableOptions.Initialize<SampleSetting>(conf =>
{
    // save file location is ./config/mysettings.json
    // extension is determined by the provider (omittable)
    conf.FilePath = "./config/mysettings";

    // this is same as above
    // conf.UseExecutableDirectory().AddFilePath("./config/mysettings");

    // if you want to standard system configration location, use conf.UseStandardSaveDirectory("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    // conf.UseStandardSaveDirectory("your-app-id").AddFilePath("appdata-setting");

    // customize the provider and file writer
    // you can use Json, Xml, Yaml, Encrypted file, or your original format by implementing IFormatProvider
    conf.FormatProvider = new JsonFormatProvider()
    {
        // if you want to keep backup files, use CommonFileProvider with BackupMaxCount > 0
        // FileProvider = new CommonFileProvider() { BackupMaxCount = 5 };

        // customize JsonSerializerOptions
        JsonSerializerOptions = { WriteIndented = true },
    };

    // if you want to use logging, set Logger
    // required NuGet package: Microsoft.Extensions.Logging.Console
    // conf.Logger = LoggerFactory
    //    .Create(builder => builder.AddConsole())
    //    .CreateLogger("UserConfig");

    // if you want to validate the config before saving, use
    // * UseDataAnnotationsValidation: use data annotation attributes in your config class. Defaults to true.
    // * WithValidatorFunction: a simple way to set validation function
    // * WithValidator: set a custom validation class implementing IValidateOptions<T>
});

// -------------------------------
// get the config instance
var options = WritableOptions.GetOptions<SampleSetting>();

// register change listener
options.OnChange(
    (setting, _) =>
    {
        Console.WriteLine("## Config changed notification received.");
        Console.WriteLine($"   New Name: {setting.Name}, LastUpdatedAt: {setting.LastUpdatedAt}");
    }
);

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
Console.WriteLine($"   at {options.GetOptionsConfiguration().ConfigFilePath}");

// get updated config instance
var updatedSampleSetting = options.CurrentValue;
Console.WriteLine(
    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
);
