using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Configuration.Writable.FileProvider;
using Configuration.Writable.Tests.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable.Tests;

public partial class AbstractionsPackageTests
{
    [OptionsModel]
    public partial class TestSettings
    {
        public string Name { get; set; } = "default";
    }

    [Fact]
    public void AbstractionsAssembly_HasOnlyFrameworkDependencies()
    {
        var assembly = typeof(IReadOnlyOptions<>).Assembly;

        assembly.GetName().Name.ShouldBe("Configuration.Writable.Abstractions");
        assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ShouldAllBe(name =>
                name == "mscorlib"
                || name == "netstandard"
                || name.StartsWith("System.", StringComparison.Ordinal)
            );
    }

    [Fact]
    public void AbstractionsAssembly_PublicApiDoesNotExposeForbiddenDependencies()
    {
        var forbiddenNamespacePrefixes = new[]
        {
            "Microsoft.Extensions",
            "System.IO.Pipelines",
            "System.Text.Json",
            "ZLogger",
            "System.ComponentModel.DataAnnotations",
        };

        var exposedTypes = typeof(IReadOnlyOptions<>)
            .Assembly.GetExportedTypes()
            .SelectMany(GetPublicSignatureTypes)
            .Where(type => type.Namespace is not null)
            .ToArray();

        exposedTypes.ShouldAllBe(type =>
            forbiddenNamespacePrefixes.All(prefix =>
                !type.Namespace!.StartsWith(prefix, StringComparison.Ordinal)
            )
        );
    }

    [Fact]
    public void ReadOnlyOptionsMonitor_DoesNotInheritMicrosoftOptionsMonitor()
    {
        typeof(IOptionsMonitor<TestSettings>)
            .IsAssignableFrom(typeof(IReadOnlyOptionsMonitor<TestSettings>))
            .ShouldBeFalse();
    }

    [Fact]
    public void CoreRegistrations_ExposeStandardCustomAndConfigurationContracts()
    {
        var fileProvider = new InMemoryFileProvider();
        var services = new ServiceCollection();
        services.AddWritableOptions<TestSettings>(options =>
        {
            options.FilePath = "settings.custom";
            options.SectionName = "Application:Settings";
            options.UseInMemoryFileProvider(fileProvider);
        });
        services.AddWritableOptions<TestSettings>(
            "named",
            options =>
            {
                options.FilePath = "named.custom";
                options.UseInMemoryFileProvider(fileProvider);
            }
        );

        using var provider = services.BuildServiceProvider();

        var standardMonitor = provider.GetRequiredService<IOptionsMonitor<TestSettings>>();
        var customMonitor = provider.GetRequiredService<IReadOnlyOptionsMonitor<TestSettings>>();
        var writableMonitor = provider.GetRequiredService<IWritableOptionsMonitor<TestSettings>>();

        provider.GetRequiredService<IOptions<TestSettings>>().ShouldNotBeNull();
        using (var scope = provider.CreateScope())
        {
            scope
                .ServiceProvider.GetRequiredService<IOptionsSnapshot<TestSettings>>()
                .ShouldNotBeNull();
        }
        provider.GetRequiredService<IReadOnlyOptions<TestSettings>>().ShouldNotBeNull();
        provider.GetRequiredService<IReadOnlyNamedOptions<TestSettings>>().ShouldNotBeNull();
        provider.GetRequiredService<IWritableOptions<TestSettings>>().ShouldNotBeNull();
        provider.GetRequiredService<IWritableNamedOptions<TestSettings>>().ShouldNotBeNull();
        writableMonitor.ShouldNotBeNull();

        customMonitor.ShouldBeAssignableTo<IOptionsMonitor<TestSettings>>();
        standardMonitor.CurrentValue.Name.ShouldBe(customMonitor.CurrentValue.Name);
        standardMonitor.Get("named").Name.ShouldBe(customMonitor.Get("named").Name);

        var info = provider
            .GetRequiredService<IOptionsConfigurationAccessor<TestSettings>>()
            .GetConfigurationInfo();
        info.InstanceName.ShouldBe(MEOptions.DefaultName);
        info.ReadPath.ShouldBe(info.WritePath);
        info.WritePath.ShouldEndWith("settings.custom");
        info.FormatFileExtension.ShouldBe("json");
        Path.GetExtension(info.WritePath).ShouldBe(".custom");
        info.SectionNameParts.ShouldBe(["Application", "Settings"]);

        var namedInfo = provider
            .GetRequiredService<INamedOptionsConfigurationAccessor<TestSettings>>()
            .GetConfigurationInfo("named");
        namedInfo.InstanceName.ShouldBe("named");
        namedInfo.WritePath.ShouldEndWith("named.custom");

        var keyedInfo = provider
            .GetRequiredKeyedService<IOptionsConfigurationAccessor<TestSettings>>("named")
            .GetConfigurationInfo();
        keyedInfo.InstanceName.ShouldBe("named");

        var coreConfiguration = provider
            .GetRequiredService<IWritableOptionsConfigurationAccessor<TestSettings>>()
            .GetOptionsConfiguration();
        coreConfiguration.ConfigFilePath.ShouldBe(info.WritePath);
    }

    [Fact]
    public async Task StandardAndCustomMonitors_ShareChangeNotificationSemantics()
    {
        var filePath = Path.Combine(
            AppContext.BaseDirectory,
            $"{Guid.NewGuid():N}.monitor-settings.json"
        );
        var services = new ServiceCollection();
        services.AddWritableOptions<TestSettings>(options =>
        {
            options.UseFile(filePath);
            options.OnChangeDebounce = TimeSpan.Zero;
        });

        using var provider = services.BuildServiceProvider();
        var standardMonitor = provider.GetRequiredService<IOptionsMonitor<TestSettings>>();
        var customMonitor = provider.GetRequiredService<IReadOnlyOptionsMonitor<TestSettings>>();
        var writableOptions = provider.GetRequiredService<IWritableOptions<TestSettings>>();
        var standardChange = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var customChange = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var standardRegistration = standardMonitor.OnChange(
            (value, _) => standardChange.TrySetResult(value.Name)
        );
        using var customRegistration = customMonitor.OnChange(
            (value, _) => customChange.TrySetResult(value.Name)
        );

        try
        {
            await writableOptions.SaveAsync(settings => settings.Name = "updated");

            (
                await FileWatcherTestHelper.WaitForConditionAsync(() =>
                    standardChange.Task.IsCompleted && customChange.Task.IsCompleted
                )
            ).ShouldBeTrue();
            (await standardChange.Task).ShouldBe("updated");
            (await customChange.Task).ShouldBe("updated");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void TestingStub_RequiresAndReturnsExplicitConfigurationInfo()
    {
        var stub = new WritableOptionsStub<TestSettings>(new TestSettings());

        Should.Throw<InvalidOperationException>(() => stub.GetConfigurationInfo());

        stub.SetConfigurationInfo(
            MEOptions.DefaultName,
            "loaded.json",
            "saved.config",
            ".json",
            ["Root"]
        );

        var info = stub.GetConfigurationInfo();
        info.ReadPath.ShouldBe("loaded.json");
        info.WritePath.ShouldBe("saved.config");
        info.FormatFileExtension.ShouldBe("json");
        info.SectionNameParts.ShouldBe(["Root"]);
    }

    private static IEnumerable<Type> GetPublicSignatureTypes(Type type)
    {
        foreach (var exposedType in ExpandType(type))
        {
            yield return exposedType;
        }

        if (type.BaseType is not null)
        {
            foreach (var exposedType in ExpandType(type.BaseType))
            {
                yield return exposedType;
            }
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            foreach (var exposedType in ExpandType(interfaceType))
            {
                yield return exposedType;
            }
        }

        foreach (
            var constraint in type.GetGenericArguments()
                .SelectMany(t => t.GetGenericParameterConstraints())
        )
        {
            foreach (var exposedType in ExpandType(constraint))
            {
                yield return exposedType;
            }
        }

        foreach (
            var memberType in type.GetMembers(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                )
                .SelectMany(GetMemberTypes)
        )
        {
            foreach (var exposedType in ExpandType(memberType))
            {
                yield return exposedType;
            }
        }
    }

    private static IEnumerable<Type> GetMemberTypes(MemberInfo member) =>
        member switch
        {
            MethodInfo method => method
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Append(method.ReturnType),
            PropertyInfo property => [property.PropertyType],
            EventInfo eventInfo when eventInfo.EventHandlerType is not null =>
            [
                eventInfo.EventHandlerType,
            ],
            FieldInfo field => [field.FieldType],
            _ => [],
        };

    private static IEnumerable<Type> ExpandType(Type type)
    {
        while (type.HasElementType)
        {
            type = type.GetElementType()!;
        }

        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            if (genericArgument.IsGenericParameter)
            {
                continue;
            }

            foreach (var exposedType in ExpandType(genericArgument))
            {
                yield return exposedType;
            }
        }
    }
}
