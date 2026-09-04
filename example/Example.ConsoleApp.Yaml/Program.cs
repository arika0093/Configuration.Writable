using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.ConsoleApp.Yaml;

// Register VYaml formatters for NativeAOT support.
// VYaml's [Preserve] attribute is not recognized by the .NET trimmer,
// so the generated __RegisterVYamlFormatter() must be called explicitly
// to prevent the formatter from being trimmed away.
SampleSetting.__RegisterVYamlFormatter();

// initialize the writable config system with YAML format
WritableOptions.Initialize(conf =>
{
    // shared configuration for all options types
    conf.FormatProvider = new YamlFormatProvider();

    // if you want to standard system configuration location, use conf.UseStandardSaveDirectory("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    // conf.UseStandardSaveDirectory("your-app-id");

    // add SampleSetting
    conf.Add<SampleSetting>(c =>
    {
        c.UseFile("./config/mysettings");
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
