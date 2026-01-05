#!/usr/bin/env dotnet
#:package Configuration.Writable@*

using System.Text.Json.Serialization;
using Configuration.Writable;
using Configuration.Writable.FormatProvider;

// initialize
WritableOptions.Initialize<SampleSetting>(conf =>
{
    conf.UseFile("usersettings.json");
    conf.FormatProvider = new JsonAotFormatProvider(SampleSettingSerializerContext.Default);
});

// get the writable options instance
var options = WritableOptions.GetOptions<SampleSetting>();

// get values
Console.WriteLine($"Current Name: {options.CurrentValue.Name}");

// optionally, you can register change callback
options.OnChange(newSetting =>
{
    Console.WriteLine($">> Settings changed! Name: {newSetting.Name}");
});

// and save to storage
Console.Write("Enter new name: ");
var newName = Console.ReadLine() ?? "";
await options.SaveAsync(setting =>
{
    setting.Name = newName;
});

// announce saved location
var savedLocation = options.GetOptionsConfiguration().ConfigFilePath;
Console.WriteLine($"Saved to {savedLocation}");

// need some delay to see the change callback in action
await Task.Delay(100);

// ------
// setting class
public partial class SampleSetting : IOptionsModel<SampleSetting>
{
    public string Name { get; set; } = "default name";
}

// source generation context for System.Text.Json serialization
[JsonSerializable(typeof(SampleSetting))]
public partial class SampleSettingSerializerContext : JsonSerializerContext;
