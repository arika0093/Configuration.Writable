using System.Threading.Tasks;

namespace Configuration.Writable.Tests;

public class WritableOptionsStubTests
{
    [Fact]
    public async Task WritableOptionsStub_UseReadonlyOptionServiceTest()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = new WritableOptionsStub<UserSettings>(setting);
        var service = new SampleReadonlyService(options);
        var act = await service.ReadName();
        act.ShouldBe("InitialName");
    }

    [Fact]
    public async Task WritableOptionsStub_UseWritableOptionServiceTest()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = new WritableOptionsStub<UserSettings>(setting);
        var service = new SampleWritableService(options);
        await service.UpdateSettings("UpdatedName", 30);
        setting.Name.ShouldBe("UpdatedName");
        setting.Age.ShouldBe(30);
    }

    [Fact]
    public async Task WritableOptionsStub_UseReadonlyOptionService_NoGenerics_Test()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = WritableOptionsStub.Create(setting);
        var service = new SampleReadonlyService(options);
        var act = await service.ReadName();
        act.ShouldBe("InitialName");
    }

    [Fact]
    public async Task WritableOptionsStub_UseWritableOptionService_NoGenerics_Test()
    {
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = WritableOptionsStub.Create(setting);
        var service = new SampleWritableService(options);
        await service.UpdateSettings("UpdatedName", 30);
        setting.Name.ShouldBe("UpdatedName");
        setting.Age.ShouldBe(30);
    }

    [Fact]
    public async Task WritableOptionsStub_OnChange_DefaultInstance_ShouldReceiveNotification()
    {
        // Arrange
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = WritableOptionsStub.Create(setting);
        UserSettings? receivedValue = null;
        var callCount = 0;

        options.OnChange(value =>
        {
            receivedValue = value;
            callCount++;
        });

        // Act
        await options.SaveAsync(s =>
        {
            s.Name = "UpdatedName";
            s.Age = 30;
        });

        // Assert
        callCount.ShouldBe(1);
        receivedValue.ShouldNotBeNull();
        receivedValue.Name.ShouldBe("UpdatedName");
        receivedValue.Age.ShouldBe(30);
    }

    [Fact]
    public async Task WritableOptionsStub_OnChange_NamedInstance_ShouldReceiveNotification()
    {
        // Arrange
        var namedValues = new System.Collections.Generic.Dictionary<string, UserSettings>
        {
            ["default"] = new UserSettings { Name = "DefaultName", Age = 20 },
            ["custom"] = new UserSettings { Name = "CustomName", Age = 25 },
        };
        var options = WritableOptionsStub.Create(namedValues);
        UserSettings? receivedValue = null;
        var callCount = 0;

        options.OnChange(
            "custom",
            value =>
            {
                receivedValue = value;
                callCount++;
            }
        );

        // Act - Update custom instance
        await options.SaveAsync(
            "custom",
            s =>
            {
                s.Name = "UpdatedCustomName";
                s.Age = 35;
            }
        );

        // Assert
        callCount.ShouldBe(1);
        receivedValue.ShouldNotBeNull();
        receivedValue.Name.ShouldBe("UpdatedCustomName");
        receivedValue.Age.ShouldBe(35);
    }

    [Fact]
    public async Task WritableOptionsStub_OnChange_NamedInstance_ShouldNotReceiveNotificationForOtherInstance()
    {
        // Arrange
        var namedValues = new System.Collections.Generic.Dictionary<string, UserSettings>
        {
            ["default"] = new UserSettings { Name = "DefaultName", Age = 20 },
            ["custom"] = new UserSettings { Name = "CustomName", Age = 25 },
        };
        var options = WritableOptionsStub.Create(namedValues);
        var callCount = 0;

        options.OnChange(
            "custom",
            _ =>
            {
                callCount++;
            }
        );

        // Act - Update default instance
        await options.SaveAsync(
            "default",
            s =>
            {
                s.Name = "UpdatedDefaultName";
                s.Age = 30;
            }
        );

        // Assert - Should not receive notification for different instance
        callCount.ShouldBe(0);
    }

    [Fact]
    public async Task WritableOptionsStub_OnChange_WithInstanceName_ShouldReceiveAllNotifications()
    {
        // Arrange
        var namedValues = new System.Collections.Generic.Dictionary<string, UserSettings>
        {
            ["default"] = new UserSettings { Name = "DefaultName", Age = 20 },
            ["custom"] = new UserSettings { Name = "CustomName", Age = 25 },
        };
        var options = WritableOptionsStub.Create(namedValues);
        var receivedNotifications = new System.Collections.Generic.List<(
            UserSettings value,
            string? name
        )>();

        options.OnChange(
            (value, name) =>
            {
                receivedNotifications.Add((value, name));
            }
        );

        // Act - Update both instances
        await options.SaveAsync("default", s => s.Name = "UpdatedDefaultName");
        await options.SaveAsync("custom", s => s.Name = "UpdatedCustomName");

        // Assert
        receivedNotifications.Count.ShouldBe(2);
        receivedNotifications[0].name.ShouldBe("default");
        receivedNotifications[0].value.Name.ShouldBe("UpdatedDefaultName");
        receivedNotifications[1].name.ShouldBe("custom");
        receivedNotifications[1].value.Name.ShouldBe("UpdatedCustomName");
    }

    [Fact]
    public async Task WritableOptionsStub_OnChange_MultipleListeners_ShouldAllReceiveNotifications()
    {
        // Arrange
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = WritableOptionsStub.Create(setting);
        var callCount1 = 0;
        var callCount2 = 0;
        var callCount3 = 0;

        options.OnChange(_ => callCount1++);
        options.OnChange((_, _) => callCount2++);
        options.OnChange("", _ => callCount3++);

        // Act
        await options.SaveAsync(s => s.Name = "UpdatedName");

        // Assert - All listeners should be called
        callCount1.ShouldBe(1);
        callCount2.ShouldBe(1);
        callCount3.ShouldBe(1);
    }

    [Fact]
    public void WritableOptionsStub_ChangeListeners_ShouldBeAccessible()
    {
        // Arrange
        var setting = new UserSettings { Name = "InitialName", Age = 20 };
        var options = WritableOptionsStub.Create(setting);

        // Act
        options.OnChange(value => { });
        options.OnChange((value, name) => { });

        // Assert
        options.ChangeListeners.Count.ShouldBe(2);
    }
}

file class SampleReadonlyService(IReadOnlyOptions<UserSettings> option)
{
    public async Task<string?> ReadName()
    {
        return await Task.FromResult(option.CurrentValue.Name);
    }
}

file class SampleWritableService(IWritableOptions<UserSettings> option)
{
    public async Task UpdateSettings(string name, int age)
    {
        await option.SaveAsync(settings =>
        {
            settings.Name = name;
            settings.Age = age;
        });
    }
}

file class UserSettings
{
    public string? Name { get; set; }
    public int Age { get; set; }
}
