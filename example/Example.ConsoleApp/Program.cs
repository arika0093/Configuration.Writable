using Configuration.Writable;
using Example.ConsoleApp;

// initialize the writable config system
// default save location is ./userconfig.json
//WritableConfig.Initialize<SampleSetting>();

// if you want to specify a custom save location, use the following instead:
WritableConfig.Initialize<SampleSetting>(options =>
{
    //options.FileName = "config/encrypt-setting";
    //options.Provider = new WritableConfigEncryptProvider("th3Rand0mP4ssw0rd!");
    options.Provider = new WritableConfigXmlProvider();
    //options.FileWriter = new CommonWriteFileProvider() { BackupMaxCount = 5 };
});

// or use system app data folder
// (e.g. C:\Users\<User>\AppData\Local\sample-console-app\appdata-setting.json on Windows):
//WritableConfig.Initialize<SampleSetting>(options =>
//{
//    options.FileName = "appdata-setting.json";
//    options.UseStandardSaveLocation("sample-console-app");
//});

// -------------------------------
// get the config instance
var sampleSetting = WritableConfig.GetValue<SampleSetting>();
Console.WriteLine($">> Name: {sampleSetting.Name}, LastUpdatedAt: {sampleSetting.LastUpdatedAt}");

// modify the config instance
Console.Write(":: Enter new name: ");
var newName = Console.ReadLine();

// save the config instance
sampleSetting.Name = newName;
sampleSetting.LastUpdatedAt = DateTime.Now;
WritableConfig.Save<SampleSetting>(sampleSetting);
Console.WriteLine(":: Config saved.");

// get updated config instance
var updatedSampleSetting = WritableConfig.GetValue<SampleSetting>();
Console.WriteLine(
    $">> Name: {updatedSampleSetting.Name}, LastUpdatedAt: {updatedSampleSetting.LastUpdatedAt}"
);
