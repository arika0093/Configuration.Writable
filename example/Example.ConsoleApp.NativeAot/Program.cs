using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.ConsoleApp.NativeAot;

// initialize the writable config system
WritableOptions.Initialize(conf =>
{
    // shared configuration for all options types

    // enable JSON schema generation for the configuration classes
    // $ dotnet run -- --cw-generate-json-schema ./schema
    conf.EnableJsonSchemaGeneration(SampleSettingSerializerContext.Default);
    conf.SchemaBaseUri = "../schema/";

    // JsonAotFormatProvider is the recommended format provider for NativeAOT scenarios
    conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);

    // if you want to standard system configuration location, use conf.UseStandardSaveDirectory("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    // conf.UseStandardSaveDirectory("your-app-id");

    // add SampleSetting
    conf.Add<SampleSetting>(c =>
    {
        c.UseFile("./config/mysettings");

        // if you want to customize the section name in the config file
        // c.SectionName = "App:SampleSetting";

        // use a custom validator to validate the config instance before saving
        // (built-in DataAnnotations validation is disabled when building for NativeAOT)
        c.WithValidator<SampleSettingValidator>();
    });
});

// -------------------------------
// get the config instance
var options = WritableOptions.GetOptions<SampleSetting>();

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
