using Configuration.Writable;
using Configuration.Writable.FormatProvider;
using Example.ConsoleApp.NativeAot;

// initialize the writable config system
// default save location is ./userconfig.json
WritableOptions.Initialize<SampleSetting>();

// if you want to specify a custom save location, use the following instead:
WritableOptions.Initialize<SampleSetting>(conf =>
{
    conf.FilePath = "./config/mysettings";

    // if you want to customize the section name in the config file
    // conf.SectionName = "App:SampleSetting";

    // customize the provider and file writer
    // JsonAotFormatProvider is the recommended provider for NativeAOT scenarios
    conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);

    // If use DataAnnotation validation with Source Generators,
    // see SampleSettingValidator class in this project and comment out below code.
    conf.UseDataAnnotationsValidation = false;
    conf.WithValidator<SampleSettingValidator>();
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
Console.WriteLine($"   at {options.GetOptionsConfiguration().ConfigFilePath}");

// get updated config instance
var updatedSampleSetting = options.CurrentValue;
Console.WriteLine(
    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
);
