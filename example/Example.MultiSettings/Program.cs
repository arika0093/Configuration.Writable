using Configuration.Writable;
using Configuration.Writable.FileProvider;

// if you want to save one file with multiple settings, you can use ZipFileProvider
var zipFileProvider = new ZipFileProvider { ZipFileName = "configurations.zip" };

// initialize each setting with the same file provider
WritableOptions.Initialize<UserSetting>(conf =>
{
    conf.FilePath = "usersettings";
    // use common file provider with zip file
    conf.FileProvider = zipFileProvider;
});
WritableOptions.Initialize<UserSecretSetting>(conf =>
{
    conf.FilePath = "secrets";
    conf.FileProvider = zipFileProvider;
    // dotnet add package Configuration.Writable.Encrypt
    conf.FormatProvider = new EncryptFormatProvider("any-encrypt-password");
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
