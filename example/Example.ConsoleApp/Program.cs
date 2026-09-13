using System.Text.Json;
using Configuration.Writable;
using Example.ConsoleApp;

// initialize the writable config system
WritableOptions.Initialize(conf =>
{
    // shared configuration for all options types

    // enable JSON schema generation for the configuration classes
    // $ dotnet run -- --cw-generate-json-schema ./schema
    conf.EnableJsonSchemaGeneration();
    conf.SchemaBaseUri = "../schema/";

    // customize the file format (Json, Yaml, or Xml via YamlFileOptions/XmlFileOptions)
    conf.FormatOptions = new JsonFileOptions
    {
        // One backup is kept by default.

        // customize JsonSerializerOptions
        JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true },
    };

    // if you want to use logging, set Logger
    // conf.Logger = LoggerFactory
    //    .Create(builder => builder.AddSimpleConsole())
    //    .CreateLogger("UserConfig");

    // if you want to standard system configuration location, use conf.UseStandardSaveDirectory("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    // conf.UseStandardSaveDirectory("your-app-id");

    // add SampleSetting
    conf.Add<SampleSetting>(c =>
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
Console.WriteLine($"  at {options.ConfigurationInfo.WritePath}");

// get updated config instance
var updatedSampleSetting = options.CurrentValue;
Console.WriteLine(
    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
);
