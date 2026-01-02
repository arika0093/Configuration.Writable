---
sidebar_position: 2
---

# Testing

Configuration.Writable provides utilities to make testing easier.

## Using Stubs

For simple test scenarios, use `WritableOptionsStub`:

```csharp
using Configuration.Writable.Testing;
using Xunit;

public class MyServiceTests
{
    [Fact]
    public void TestService()
    {
        // Arrange
        var settingValue = new UserSetting
        {
            Name = "Test User",
            Age = 25
        };
        var options = WritableOptionsStub.Create(settingValue);
        var service = new MyService(options);
        
        // Act
        service.DoSomething();
        
        // Assert
        Assert.Equal("Expected Name", settingValue.Name);
    }
}
```

The stub automatically updates the provided instance when `SaveAsync` is called:

```csharp
[Fact]
public async Task TestSaveChanges()
{
    // Arrange
    var setting = new UserSetting { Name = "Original" };
    var options = WritableOptionsStub.Create(setting);
    
    // Act
    await options.SaveAsync(s => s.Name = "Updated");
    
    // Assert
    Assert.Equal("Updated", setting.Name);
    Assert.Equal("Updated", options.CurrentValue.Name);
}
```

## File System Testing

For tests that require actual file I/O, use `WritableOptionsSimpleInstance`:

```csharp
using Configuration.Writable.Testing;
using Xunit;

public class FileSystemTests
{
    [Fact]
    public async Task TestWithRealFiles()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var instance = new WritableOptionsSimpleInstance<UserSetting>();
        
        instance.Initialize(conf =>
        {
            conf.UseFile(tempFile);
        });
        
        var options = instance.GetOptions();
        
        // Act
        await options.SaveAsync(s =>
        {
            s.Name = "Test User";
            s.Age = 30;
        });
        
        // Assert
        var json = await File.ReadAllTextAsync(tempFile);
        Assert.Contains("Test User", json);
        Assert.Contains("30", json);
        
        // Cleanup
        File.Delete(tempFile);
    }
}
```

## Testing with DI

When testing services that use DI:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class DIServiceTests
{
    [Fact]
    public async Task TestWithDI()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddWritableOptions<UserSetting>(conf =>
        {
            conf.UseFile(Path.GetTempFileName());
        });
        
        services.AddSingleton<MyService>();
        
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<MyService>();
        
        // Act & Assert
        await service.DoSomethingAsync();
        
        var options = provider.GetRequiredService<IReadOnlyOptions<UserSetting>>();
        Assert.Equal("Expected Value", options.CurrentValue.Name);
    }
}
```

## Testing Change Detection

Test that your code responds to configuration changes:

```csharp
[Fact]
public async Task TestChangeDetection()
{
    // Arrange
    var setting = new UserSetting { Name = "Original" };
    var options = WritableOptionsStub.Create(setting);
    
    var changeDetected = false;
    var newName = "";
    
    options.OnChange(s =>
    {
        changeDetected = true;
        newName = s.Name;
    });
    
    // Act
    await options.SaveAsync(s => s.Name = "Changed");
    
    // Assert
    Assert.True(changeDetected);
    Assert.Equal("Changed", newName);
}
```

## Testing Validation

Test validation behavior:

```csharp
using Microsoft.Extensions.Options;
using Xunit;

[Fact]
public async Task TestValidationFailure()
{
    // Arrange
    var services = new ServiceCollection();
    
    services.AddWritableOptions<UserSetting>(conf =>
    {
        conf.UseFile(Path.GetTempFileName());
        conf.UseDataAnnotationsValidation = true;
    });
    
    var provider = services.BuildServiceProvider();
    var options = provider.GetRequiredService<IWritableOptions<UserSetting>>();
    
    // Act & Assert
    await Assert.ThrowsAsync<OptionsValidationException>(async () =>
    {
        await options.SaveAsync(s =>
        {
            s.Name = "ab"; // Too short - violates MinLength(3)
        });
    });
}
```

## Mock Alternative

You can also use mocking frameworks:

```csharp
using Moq;
using Xunit;

[Fact]
public void TestWithMock()
{
    // Arrange
    var mockOptions = new Mock<IReadOnlyOptions<UserSetting>>();
    mockOptions
        .Setup(o => o.CurrentValue)
        .Returns(new UserSetting { Name = "Mocked", Age = 25 });
    
    var service = new MyService(mockOptions.Object);
    
    // Act
    var result = service.GetUserName();
    
    // Assert
    Assert.Equal("Mocked", result);
}
```

## Testing Named Instances

```csharp
[Fact]
public async Task TestNamedInstances()
{
    // Arrange
    var services = new ServiceCollection();
    
    services.AddWritableOptions<UserSetting>("First", conf =>
    {
        conf.UseFile(Path.GetTempFileName());
    });
    
    services.AddWritableOptions<UserSetting>("Second", conf =>
    {
        conf.UseFile(Path.GetTempFileName());
    });
    
    var provider = services.BuildServiceProvider();
    var options = provider.GetRequiredService<IWritableNamedOptions<UserSetting>>();
    
    // Act
    await options.SaveAsync("First", s => s.Name = "First Name");
    await options.SaveAsync("Second", s => s.Name = "Second Name");
    
    // Assert
    Assert.Equal("First Name", options.Get("First").Name);
    Assert.Equal("Second Name", options.Get("Second").Name);
}
```

## Best Practices

1. **Use stubs for unit tests** - Fast and isolated
2. **Use real files for integration tests** - Validate actual behavior
3. **Clean up temp files** - Always delete test files
4. **Test validation** - Ensure validation works as expected
5. **Test change detection** - Verify callbacks work

## Example: Complete Test Class

```csharp
using Configuration.Writable;
using Configuration.Writable.Testing;
using Microsoft.Extensions.Options;
using Xunit;

public class UserServiceTests : IDisposable
{
    private readonly string _tempFile;
    
    public UserServiceTests()
    {
        _tempFile = Path.GetTempFileName();
    }
    
    [Fact]
    public void GetCurrentUser_ReturnsName()
    {
        // Arrange
        var setting = new UserSetting { Name = "John" };
        var options = WritableOptionsStub.Create(setting);
        var service = new UserService(options);
        
        // Act
        var name = service.GetCurrentUserName();
        
        // Assert
        Assert.Equal("John", name);
    }
    
    [Fact]
    public async Task UpdateUser_SavesCorrectly()
    {
        // Arrange
        var setting = new UserSetting { Name = "John", Age = 25 };
        var options = WritableOptionsStub.Create(setting);
        var service = new UserService(options);
        
        // Act
        await service.UpdateUserNameAsync("Jane");
        
        // Assert
        Assert.Equal("Jane", setting.Name);
    }
    
    [Fact]
    public async Task SaveToFile_WritesCorrectJson()
    {
        // Arrange
        var instance = new WritableOptionsSimpleInstance<UserSetting>();
        instance.Initialize(conf => conf.UseFile(_tempFile));
        var options = instance.GetOptions();
        
        // Act
        await options.SaveAsync(s =>
        {
            s.Name = "FileTest";
            s.Age = 30;
        });
        
        // Assert
        var json = await File.ReadAllTextAsync(_tempFile);
        Assert.Contains("FileTest", json);
        Assert.Contains("30", json);
    }
    
    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}
```

## Next Steps

- [NativeAOT](./native-aot) - Test NativeAOT applications
- [Validation](../customization/validation) - Validation testing patterns
- [API Reference](../api/interfaces) - Interface documentation
