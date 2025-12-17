using System;
using System.IO;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Configuration.Writable.Tests;

public class KeyedServicesIntegrationTests
{
    private readonly InMemoryFileProvider _FileProvider = new();

    public class AppSettings
    {
        public string ApplicationName { get; set; } = "DefaultApp";
        public int Port { get; set; } = 8080;
        public bool EnableLogging { get; set; } = true;
    }

    [Fact]
    public void AddWritableOptions_WithInstanceName_ShouldRegisterKeyedServices()
    {
        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "Production";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve keyed services
        var writableOptions = serviceProvider.GetKeyedService<IWritableOptions<AppSettings>>("Production");
        var readonlyOptions = serviceProvider.GetKeyedService<IReadOnlyOptions<AppSettings>>("Production");

        writableOptions.ShouldNotBeNull();
        readonlyOptions.ShouldNotBeNull();
    }

    [Fact]
    public void AddWritableOptions_WithoutInstanceName_ShouldNotRegisterKeyedServices()
    {
        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Should NOT be able to resolve keyed services when no instance name
        var writableOptions = serviceProvider.GetKeyedService<IWritableOptions<AppSettings>>("");
        var readonlyOptions = serviceProvider.GetKeyedService<IReadOnlyOptions<AppSettings>>("");

        writableOptions.ShouldBeNull();
        readonlyOptions.ShouldBeNull();
    }

    [Fact]
    public void AddWritableOptions_WithEmptyInstanceName_ShouldNotRegisterKeyedServices()
    {
        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Should NOT register keyed services with empty instance name
        var writableOptions = serviceProvider.GetKeyedService<IWritableOptions<AppSettings>>("");
        writableOptions.ShouldBeNull();
    }

    [Fact]
    public void AddWritableOptions_WithMultipleInstances_ShouldRegisterMultipleKeyedServices()
    {
        var services = new ServiceCollection();

        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "Development";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "Production";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();

        // Should be able to resolve both keyed services
        var devOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("Development");
        var prodOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("Production");

        devOptions.ShouldNotBeNull();
        prodOptions.ShouldNotBeNull();

        // They should be different instances
        devOptions.ShouldNotBeSameAs(prodOptions);
    }

    [Fact]
    public async Task KeyedWritableOptions_CurrentValue_ShouldReturnCorrectValue()
    {
        var fileName = Path.GetRandomFileName();

        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "TestInstance";
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("TestInstance");

        // Save a value
        await writableOptions.SaveAsync(settings =>
        {
            settings.ApplicationName = "KeyedApp";
            settings.Port = 9000;
        });

        // CurrentValue should return the saved value
        var current = writableOptions.CurrentValue;
        current.ApplicationName.ShouldBe("KeyedApp");
        current.Port.ShouldBe(9000);
    }

    [Fact]
    public async Task KeyedWritableOptions_SaveAsync_ShouldPersistData()
    {
        var fileName = Path.GetRandomFileName();

        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "SaveTest";
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("SaveTest");

        var newSettings = new AppSettings
        {
            ApplicationName = "SavedApp",
            Port = 7000,
            EnableLogging = false
        };

        await writableOptions.SaveAsync(newSettings);

        _FileProvider.FileExists(fileName).ShouldBeTrue();

        var currentValue = writableOptions.CurrentValue;
        currentValue.ApplicationName.ShouldBe("SavedApp");
        currentValue.Port.ShouldBe(7000);
        currentValue.EnableLogging.ShouldBeFalse();
    }

    [Fact]
    public async Task KeyedWritableOptions_SaveAsyncWithAction_ShouldUpdateData()
    {
        var fileName = Path.GetRandomFileName();

        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "ActionTest";
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("ActionTest");

        await writableOptions.SaveAsync(settings =>
        {
            settings.ApplicationName = "UpdatedApp";
            settings.Port = 8888;
        });

        _FileProvider.FileExists(fileName).ShouldBeTrue();

        var currentValue = writableOptions.CurrentValue;
        currentValue.ApplicationName.ShouldBe("UpdatedApp");
        currentValue.Port.ShouldBe(8888);
    }

    [Fact]
    public async Task KeyedReadOnlyOptions_ShouldProvideReadAccess()
    {
        var fileName = Path.GetRandomFileName();

        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "ReadOnlyTest";
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();

        var writableOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("ReadOnlyTest");
        var readonlyOptions = serviceProvider.GetRequiredKeyedService<IReadOnlyOptions<AppSettings>>("ReadOnlyTest");

        // Save via writable options
        await writableOptions.SaveAsync(settings =>
        {
            settings.ApplicationName = "ReadOnlyApp";
        });

        // Read via readonly options
        var value = readonlyOptions.CurrentValue;
        value.ApplicationName.ShouldBe("ReadOnlyApp");
    }

    [Fact]
    public async Task KeyedOptions_WithSeparateInstances_ShouldMaintainIndependence()
    {
        var file1 = Path.GetRandomFileName();
        var file2 = Path.GetRandomFileName();

        var services = new ServiceCollection();

        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "Instance1";
            options.FilePath = file1;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "Instance2";
            options.FilePath = file2;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();

        var options1 = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("Instance1");
        var options2 = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("Instance2");

        // Save different values to each instance
        await options1.SaveAsync(settings => settings.ApplicationName = "App1");
        await options2.SaveAsync(settings => settings.ApplicationName = "App2");

        // Verify independence
        options1.CurrentValue.ApplicationName.ShouldBe("App1");
        options2.CurrentValue.ApplicationName.ShouldBe("App2");
    }

    [Fact]
    public void KeyedOptions_GetConfigurationOptions_ShouldReturnCorrectConfiguration()
    {
        var fileName = Path.GetRandomFileName();

        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "ConfigTest";
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("ConfigTest");

        var configOptions = writableOptions.GetConfigurationOptions();

        configOptions.ShouldNotBeNull();
        configOptions.InstanceName.ShouldBe("ConfigTest");
        configOptions.ConfigFilePath.ShouldEndWith(fileName);
    }

    [Fact]
    public async Task KeyedOptions_OnChange_ShouldReceiveNotifications()
    {
        var fileName = Path.GetRandomFileName();
        var changeNotified = false;
        string? notifiedName = null;

        var services = new ServiceCollection();
        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "ChangeTest";
            options.FilePath = fileName;
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();
        var writableOptions = serviceProvider.GetRequiredKeyedService<IWritableOptions<AppSettings>>("ChangeTest");

        using var changeToken = writableOptions.OnChange((settings, name) =>
        {
            changeNotified = true;
            notifiedName = name;
        });

        // Trigger a change through the underlying named options
        var namedOptions = serviceProvider.GetRequiredService<IWritableNamedOptions<AppSettings>>();
        await namedOptions.SaveAsync("ChangeTest", settings => settings.ApplicationName = "Changed");

        // Give time for change notification to propagate
        await Task.Delay(100);

        changeNotified.ShouldBeTrue();
    }

    [Fact]
    public void KeyedServices_WithDifferentKeys_ShouldBeIndependent()
    {
        var services = new ServiceCollection();

        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "KeyA";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "KeyB";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        services.AddWritableOptions<AppSettings>(options =>
        {
            options.InstanceName = "KeyC";
            options.UseInMemoryFileProvider(_FileProvider);
        });

        var serviceProvider = services.BuildServiceProvider();

        var optionsA = serviceProvider.GetKeyedService<IWritableOptions<AppSettings>>("KeyA");
        var optionsB = serviceProvider.GetKeyedService<IWritableOptions<AppSettings>>("KeyB");
        var optionsC = serviceProvider.GetKeyedService<IWritableOptions<AppSettings>>("KeyC");
        var optionsNonExistent = serviceProvider.GetKeyedService<IWritableOptions<AppSettings>>("NonExistent");

        optionsA.ShouldNotBeNull();
        optionsB.ShouldNotBeNull();
        optionsC.ShouldNotBeNull();
        optionsNonExistent.ShouldBeNull();

        optionsA.ShouldNotBeSameAs(optionsB);
        optionsB.ShouldNotBeSameAs(optionsC);
        optionsA.ShouldNotBeSameAs(optionsC);
    }
}
