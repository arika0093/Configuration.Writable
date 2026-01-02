using Configuration.Writable;

// initialize each setting with the same file provider
WritableOptions.Initialize<UserSetting>(conf =>
{
    conf.UseFile("usersettings");
    // use common file provider with zip file
    conf.SectionName = "UserSettings";
});
WritableOptions.Initialize<UserSecretSetting>(conf =>
{
    conf.UseFile("usersettings");
    conf.SectionName = "Secrets";
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
