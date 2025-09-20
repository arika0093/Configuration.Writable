using Configuration.Writable;
using Example.ConsoleApp;

// initialize the writable config system
// default save location is ./userconfig.json
WritableConfig<SampleSetting>.Initialize();

// if you want to specify a custom save location, use the following instead:
//WritableConfig<SampleSetting>.Initialize(options =>
//{
//    options.FileName = "../custom-setting.json";
//});

// or use system app data folder
// (e.g. C:\Users\<User>\AppData\Local\sample-console-app\appdata-setting.json on Windows):
//WritableConfig<SampleSetting>.Initialize(options =>
//{
//    options.FileName = "appdata-setting.json";
//    options.UseStandardSaveLocation("sample-console-app");
//});

// -------------------------------
// get the config instance
var sampleSetting = WritableConfig<SampleSetting>.GetValue();
Console.WriteLine($">> Name: {sampleSetting.Name}, LastUpdatedAt: {sampleSetting.LastUpdatedAt}");

// modify the config instance
Console.Write(":: Enter new name: ");
var newName = Console.ReadLine();

// save the config instance
sampleSetting.Name = newName;
sampleSetting.LastUpdatedAt = DateTime.Now;
WritableConfig<SampleSetting>.Save(sampleSetting);
Console.WriteLine(":: Config saved.");

// get updated config instance
var updatedSampleSetting = WritableConfig<SampleSetting>.GetValue();
Console.WriteLine(
    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
);
