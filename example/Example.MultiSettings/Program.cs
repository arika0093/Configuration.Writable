using Configuration.Writable;

// initialize each setting with shared configuration
WritableOptions.Initialize(conf =>
{
    // shared configuration for all options types
    // when using SectionName to store multiple settings in one file, set the file path here
    conf.UseFile("usersettings");

    // if you want to standard system configuration location, use conf.UseStandardSaveDirectory("your-app-id");
    // e.g. %APPDATA%\your-app-id\appdata-setting.json on Windows
    // conf.UseStandardSaveDirectory("your-app-id");

    // add UserSetting
    conf.Add<UserSetting>(c =>
    {
        c.SectionName = "UserSettings";
    });

    // add UserSecretSetting
    conf.Add<UserSecretSetting>(c =>
    {
        c.SectionName = "Secrets";
    });
});

// and get each setting
var userOptions = WritableOptions.GetOptions<UserSetting>();
var secretOptions = WritableOptions.GetOptions<UserSecretSetting>();

// get value
var user = userOptions.CurrentValue;
var secret = secretOptions.CurrentValue;
Console.WriteLine($"Name: {user.Name}, Age: {user.Age}");
Console.WriteLine($"Password: {secret.Password}");

// set value and save to file
await userOptions.SaveAsync(setting =>
{
    setting.Name = $"new name at {DateTime.Now:HH:mm:ss}";
    setting.Age = Random.Shared.Next(10, 100);
});
await secretOptions.SaveAsync(setting =>
{
    setting.Password = $"new password at {DateTime.Now:HH:mm:ss}";
});

// -------------------

public class UserSetting
{
    public string Name { get; set; } = "default name";
    public int Age { get; set; } = 20;
}

public class UserSecretSetting
{
    public string Password { get; set; } = "";
}
