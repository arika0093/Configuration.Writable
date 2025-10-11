using Configuration.Writable;

WritableConfig.Initialize<UserSetting>(opt =>
{
    opt.FilePath = "usersettings";
});
WritableConfig.Initialize<UserSecretSetting>(opt =>
{
    opt.FilePath = "my-secret-folder/secrets";
    // dotnet add package Configuration.Writable.Encrypt
    opt.Provider = new WritableConfigEncryptProvider("any-encrypt-password");
});

// and get each setting
var userOptions = WritableConfig.GetOptions<UserSetting>();
var secretOptions = WritableConfig.GetOptions<UserSecretSetting>();

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
