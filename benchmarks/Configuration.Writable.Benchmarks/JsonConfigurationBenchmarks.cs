using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;
using BenchmarkDotNet.Attributes;
using VYaml.Annotations;
using VYaml.Serialization;

namespace Configuration.Writable.Benchmarks;

[MemoryDiagnoser]
public abstract class JsonConfigurationBenchmarkBase
{
    private const string SectionName = "Settings";
    private string _dataDirectory = null!;
    private string _publishedFilePath = null!;
    private string _localFilePath = null!;
    private BenchmarkConfiguration _fullConfiguration = null!;
    private BenchmarkSection _sectionConfiguration = null!;
    private string _initialJson = null!;

    private ConfigurationWritableRunner _publishedPackage = null!;
    private ConfigurationWritableRunner _localSource = null!;

    [Params(4, 64, 512)]
    public int ItemCount { get; set; }

    protected abstract bool IsPartial { get; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "benchmark-data", GetType().Name);
        RecreateDirectory(_dataDirectory);

        _publishedFilePath = Path.Combine(_dataDirectory, "published.json");
        _localFilePath = Path.Combine(_dataDirectory, "local.json");
        _fullConfiguration = BenchmarkConfiguration.Create(ItemCount);
        _sectionConfiguration = BenchmarkSection.Create(ItemCount);
        _initialJson = IsPartial
            ? JsonSerializer.Serialize(
                new PartialConfigurationFile
                {
                    Settings = _sectionConfiguration,
                    Preserved = BenchmarkConfiguration.Create(ItemCount),
                }
            )
            : JsonSerializer.Serialize(_fullConfiguration);

        _publishedPackage = ConfigurationWritableRunner.CreatePublishedPackage(
            _publishedFilePath,
            IsPartial ? SectionName : string.Empty
        );
        _localSource = ConfigurationWritableRunner.CreateLocalSource(
            _localFilePath,
            IsPartial ? SectionName : string.Empty
        );
    }

    [IterationSetup]
    public void IterationSetup()
    {
        File.WriteAllText(_publishedFilePath, _initialJson);
        File.WriteAllText(_localFilePath, _initialJson);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    protected BenchmarkConfiguration FullConfiguration => _fullConfiguration;

    protected BenchmarkSection SectionConfiguration => _sectionConfiguration;

    protected Task<long> SavePublishedPackageAsync<T>(T configuration)
        where T : class, new() => _publishedPackage.SaveAsync(configuration);

    protected Task<long> SaveLocalSourceAsync<T>(T configuration)
        where T : class, new() => _localSource.SaveAsync(configuration);

    protected object LoadPublishedPackage() => _publishedPackage.Load();

    protected object LoadLocalSource() => _localSource.Load();

    private static void RecreateDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);
    }
}

public class JsonFullSaveBenchmarks : JsonConfigurationBenchmarkBase
{
    protected override bool IsPartial => false;

    [Benchmark(Baseline = true)]
    public Task<long> PublishedPackageSave() => SavePublishedPackageAsync(FullConfiguration);

    [Benchmark]
    public Task<long> LocalSourceSave() => SaveLocalSourceAsync(FullConfiguration);
}

public class JsonFullLoadBenchmarks : JsonConfigurationBenchmarkBase
{
    protected override bool IsPartial => false;

    [Benchmark(Baseline = true)]
    public int PublishedPackageLoad() => ConfigurationChecksum.From(LoadPublishedPackage());

    [Benchmark]
    public int LocalSourceLoad() => ConfigurationChecksum.From(LoadLocalSource());
}

public class JsonPartialSaveBenchmarks : JsonConfigurationBenchmarkBase
{
    protected override bool IsPartial => true;

    [Benchmark(Baseline = true)]
    public Task<long> PublishedPackageSave() => SavePublishedPackageAsync(SectionConfiguration);

    [Benchmark]
    public Task<long> LocalSourceSave() => SaveLocalSourceAsync(SectionConfiguration);
}

public class JsonPartialLoadBenchmarks : JsonConfigurationBenchmarkBase
{
    protected override bool IsPartial => true;

    [Benchmark(Baseline = true)]
    public int PublishedPackageLoad() => ConfigurationChecksum.From(LoadPublishedPackage());

    [Benchmark]
    public int LocalSourceLoad() => ConfigurationChecksum.From(LoadLocalSource());
}

public abstract class ProviderConfigurationBenchmarkBase
{
    private string _dataDirectory = null!;
    private string _publishedFilePath = null!;
    private string _localFilePath = null!;
    private ProviderBenchmarkConfiguration _fullConfiguration = null!;
    private BenchmarkSection _sectionConfiguration = null!;
    private string _initialContents = null!;

    private ConfigurationWritableRunner _publishedPackage = null!;
    private ConfigurationWritableRunner _localSource = null!;

    [Params(4, 64, 512)]
    public int ItemCount { get; set; }

    protected abstract ProviderBenchmarkFormat Format { get; }

    protected abstract bool IsPartial { get; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "benchmark-data", GetType().Name);
        RecreateDirectory(_dataDirectory);

        _publishedFilePath = Path.Combine(_dataDirectory, $"published.{Format.FileExtension}");
        _localFilePath = Path.Combine(_dataDirectory, $"local.{Format.FileExtension}");
        _fullConfiguration = ProviderBenchmarkConfiguration.Create(ItemCount);
        _sectionConfiguration = BenchmarkSection.Create(ItemCount);
        _initialContents = ProviderBenchmarkData.Serialize(
            Format,
            IsPartial
                ? new ProviderPartialConfigurationFile
                {
                    Settings = _sectionConfiguration,
                    Preserved = ProviderBenchmarkConfiguration.Create(ItemCount),
                }
                : _fullConfiguration
        );

        _publishedPackage = ConfigurationWritableRunner.CreatePublishedProvider(
            _publishedFilePath,
            IsPartial ? Format.SectionName : string.Empty,
            Format
        );
        _localSource = ConfigurationWritableRunner.CreateLocalProvider(
            _localFilePath,
            IsPartial ? Format.SectionName : string.Empty,
            Format
        );
    }

    [IterationSetup]
    public void IterationSetup()
    {
        File.WriteAllText(_publishedFilePath, _initialContents);
        File.WriteAllText(_localFilePath, _initialContents);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    protected ProviderBenchmarkConfiguration FullConfiguration => _fullConfiguration;

    protected BenchmarkSection SectionConfiguration => _sectionConfiguration;

    protected Task<long> SavePublishedPackageAsync<T>(T configuration)
        where T : class, new() => _publishedPackage.SaveAsync(configuration);

    protected Task<long> SaveLocalSourceAsync<T>(T configuration)
        where T : class, new() => _localSource.SaveAsync(configuration);

    protected object LoadPublishedPackage() => _publishedPackage.Load();

    protected object LoadLocalSource() => _localSource.Load();

    private static void RecreateDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        Directory.CreateDirectory(directory);
    }
}

public class XmlFullSaveBenchmarks : ProviderConfigurationBenchmarkBase
{
    protected override ProviderBenchmarkFormat Format => ProviderBenchmarkFormat.Xml;

    protected override bool IsPartial => false;

    [Benchmark(Baseline = true)]
    public Task<long> PublishedPackageSave() => SavePublishedPackageAsync(FullConfiguration);

    [Benchmark]
    public Task<long> LocalSourceSave() => SaveLocalSourceAsync(FullConfiguration);
}

public class XmlFullLoadBenchmarks : ProviderConfigurationBenchmarkBase
{
    protected override ProviderBenchmarkFormat Format => ProviderBenchmarkFormat.Xml;

    protected override bool IsPartial => false;

    [Benchmark(Baseline = true)]
    public int PublishedPackageLoad() => ConfigurationChecksum.From(LoadPublishedPackage());

    [Benchmark]
    public int LocalSourceLoad() => ConfigurationChecksum.From(LoadLocalSource());
}

public class XmlPartialSaveBenchmarks : ProviderConfigurationBenchmarkBase
{
    protected override ProviderBenchmarkFormat Format => ProviderBenchmarkFormat.Xml;

    protected override bool IsPartial => true;

    [Benchmark(Baseline = true)]
    public Task<long> PublishedPackageSave() => SavePublishedPackageAsync(SectionConfiguration);

    [Benchmark]
    public Task<long> LocalSourceSave() => SaveLocalSourceAsync(SectionConfiguration);
}

public class YamlFullSaveBenchmarks : ProviderConfigurationBenchmarkBase
{
    protected override ProviderBenchmarkFormat Format => ProviderBenchmarkFormat.Yaml;

    protected override bool IsPartial => false;

    [Benchmark(Baseline = true)]
    public Task<long> PublishedPackageSave() => SavePublishedPackageAsync(FullConfiguration);

    [Benchmark]
    public Task<long> LocalSourceSave() => SaveLocalSourceAsync(FullConfiguration);
}

public class YamlFullLoadBenchmarks : ProviderConfigurationBenchmarkBase
{
    protected override ProviderBenchmarkFormat Format => ProviderBenchmarkFormat.Yaml;

    protected override bool IsPartial => false;

    [Benchmark(Baseline = true)]
    public int PublishedPackageLoad() => ConfigurationChecksum.From(LoadPublishedPackage());

    [Benchmark]
    public int LocalSourceLoad() => ConfigurationChecksum.From(LoadLocalSource());
}

public class YamlPartialSaveBenchmarks : ProviderConfigurationBenchmarkBase
{
    protected override ProviderBenchmarkFormat Format => ProviderBenchmarkFormat.Yaml;

    protected override bool IsPartial => true;

    [Benchmark(Baseline = true)]
    public Task<long> PublishedPackageSave() => SavePublishedPackageAsync(SectionConfiguration);

    [Benchmark]
    public Task<long> LocalSourceSave() => SaveLocalSourceAsync(SectionConfiguration);
}

internal sealed class ConfigurationWritableRunner
{
    private readonly object _formatProvider;
    private readonly object _optionsConfiguration;
    private readonly MethodInfo _saveAsync;
    private readonly MethodInfo _loadConfiguration;
    private readonly string _filePath;
    private readonly Type _configurationType;

    private ConfigurationWritableRunner(
        object formatProvider,
        object optionsConfiguration,
        MethodInfo saveAsync,
        MethodInfo loadConfiguration,
        string filePath,
        Type configurationType
    )
    {
        _formatProvider = formatProvider;
        _optionsConfiguration = optionsConfiguration;
        _saveAsync = saveAsync;
        _loadConfiguration = loadConfiguration;
        _filePath = filePath;
        _configurationType = configurationType;
    }

    public static ConfigurationWritableRunner CreatePublishedPackage(
        string filePath,
        string sectionName
    ) =>
        Create(
            Path.Combine(AppContext.BaseDirectory, "Configuration.Writable.dll"),
            filePath,
            sectionName,
            null,
            null
        );

    public static ConfigurationWritableRunner CreateLocalSource(
        string filePath,
        string sectionName
    ) =>
        Create(
            Path.Combine(AppContext.BaseDirectory, "local", "Configuration.Writable.dll"),
            filePath,
            sectionName,
            null,
            null
        );

    public static ConfigurationWritableRunner CreatePublishedProvider(
        string filePath,
        string sectionName,
        ProviderBenchmarkFormat format
    ) =>
        Create(
            Path.Combine(AppContext.BaseDirectory, format.AssemblyFileName),
            filePath,
            sectionName,
            format.ProviderTypeName,
            typeof(ProviderBenchmarkConfiguration)
        );

    public static ConfigurationWritableRunner CreateLocalProvider(
        string filePath,
        string sectionName,
        ProviderBenchmarkFormat format
    ) =>
        Create(
            Path.Combine(AppContext.BaseDirectory, "local", format.AssemblyFileName),
            filePath,
            sectionName,
            format.ProviderTypeName,
            typeof(ProviderBenchmarkConfiguration)
        );

    public async Task<long> SaveAsync<T>(T configuration)
        where T : class, new()
    {
        var task = (Task)
            _saveAsync
                .MakeGenericMethod(typeof(T))
                .Invoke(
                    _formatProvider,
                    [configuration, _optionsConfiguration, CancellationToken.None]
                )!;

        await task.ConfigureAwait(false);
        return new FileInfo(_filePath).Length;
    }

    public object Load() =>
        _loadConfiguration.Invoke(_formatProvider, [_configurationType, _optionsConfiguration])!;

    private static ConfigurationWritableRunner Create(
        string assemblyPath,
        string filePath,
        string sectionName,
        string? providerTypeName,
        Type? fullConfigurationType
    )
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException(
                $"Configuration.Writable implementation was not copied to '{assemblyPath}'.",
                assemblyPath
            );
        }

        var loadContext = new ImplementationLoadContext(Path.GetDirectoryName(assemblyPath)!);
        var writableAssembly = loadContext.LoadFromAssemblyPath(assemblyPath);
        var coreAssembly = loadContext.LoadFromAssemblyName(
            new AssemblyName("Configuration.Writable.Core")
        );

        var builderDefinition = coreAssembly.GetType(
            "Configuration.Writable.Configure.WritableOptionsConfigBuilder`1",
            throwOnError: true
        )!;
        var instanceDefinition = coreAssembly.GetType(
            "Configuration.Writable.Testing.WritableOptionsSimpleInstance`1",
            throwOnError: true
        )!;
        var configurationType =
            sectionName.Length == 0
                ? fullConfigurationType ?? typeof(BenchmarkConfiguration)
                : typeof(BenchmarkSection);
        var builderType = builderDefinition.MakeGenericType(configurationType);
        var instanceType = instanceDefinition.MakeGenericType(configurationType);
        var instance = Activator.CreateInstance(instanceType)!;
        var formatProvider = providerTypeName is null
            ? null
            : Activator.CreateInstance(
                writableAssembly.GetType(providerTypeName, throwOnError: true)!
            )!;
        var configure = CreateConfigureDelegate(builderType, filePath, sectionName, formatProvider);
        instanceType.GetMethod("Initialize", [configure.GetType()])!.Invoke(instance, [configure]);

        var options = instanceType.GetMethod("GetOptions")!.Invoke(instance, null)!;
        var optionsConfiguration = options
            .GetType()
            .GetMethod("GetOptionsConfiguration", Type.EmptyTypes)!
            .Invoke(options, null)!;
        var configuredFormatProvider = optionsConfiguration
            .GetType()
            .GetProperty("FormatProvider")!
            .GetValue(optionsConfiguration)!;
        var formatProviderType = configuredFormatProvider.GetType();
        var saveAsync = formatProviderType
            .GetMethods()
            .First(method =>
                method.Name == "SaveAsync"
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 1
                && method.GetParameters().Length == 3
            );
        var loadConfiguration = formatProviderType.GetMethod(
            "LoadConfiguration",
            [
                typeof(Type),
                coreAssembly.GetType("Configuration.Writable.IWritableOptionsConfiguration")!,
            ]
        )!;

        _ = writableAssembly;
        return new ConfigurationWritableRunner(
            configuredFormatProvider,
            optionsConfiguration,
            saveAsync,
            loadConfiguration,
            filePath,
            configurationType
        );
    }

    private static Delegate CreateConfigureDelegate(
        Type builderType,
        string filePath,
        string sectionName,
        object? formatProvider
    )
    {
        var builder = Expression.Parameter(builderType);
        var assignments = new List<Expression>
        {
            Expression.Assign(
                Expression.Property(builder, "FilePath"),
                Expression.Constant(filePath)
            ),
            Expression.Assign(
                Expression.Property(builder, "SectionName"),
                Expression.Constant(sectionName)
            ),
            Expression.Assign(
                Expression.Property(builder, "UseDataAnnotationsValidation"),
                Expression.Constant(false)
            ),
        };
        if (formatProvider is not null)
        {
            assignments.Add(
                Expression.Assign(
                    Expression.Property(builder, "FormatProvider"),
                    Expression.Convert(
                        Expression.Constant(formatProvider),
                        Expression.Property(builder, "FormatProvider").Type
                    )
                )
            );
        }
        var actionType = typeof(Action<>).MakeGenericType(builderType);
        return Expression.Lambda(actionType, Expression.Block(assignments), builder).Compile();
    }

    private sealed class ImplementationLoadContext(string directory)
        : AssemblyLoadContext(isCollectible: false)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var candidate = Path.Combine(directory, $"{assemblyName.Name}.dll");
            return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
        }
    }
}

public sealed class BenchmarkConfiguration
{
    public int Version { get; set; } = 1;

    public string Name { get; set; } = "Configuration.Writable benchmark";

    public Dictionary<string, BenchmarkSection> Sections { get; set; } = [];

    public static BenchmarkConfiguration Create(int itemCount) =>
        new()
        {
            Sections = Enumerable
                .Range(0, itemCount)
                .ToDictionary(index => $"section-{index:D4}", _ => BenchmarkSection.Create(4)),
        };
}

[YamlObject]
public sealed partial class BenchmarkSection
{
    public string Name { get; set; } = string.Empty;

    public List<BenchmarkItem> Items { get; set; } = [];

    public static BenchmarkSection Create(int itemCount) =>
        new()
        {
            Name = $"section-with-{itemCount}-items",
            Items = Enumerable
                .Range(0, itemCount)
                .Select(index => new BenchmarkItem
                {
                    Id = index,
                    Name = $"setting-{index:D4}",
                    Value = $"value-{index:D4}-{new string('x', 48)}",
                    Enabled = index % 2 == 0,
                })
                .ToList(),
        };
}

[YamlObject]
public sealed partial class BenchmarkItem
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}

public sealed class PartialConfigurationFile
{
    public BenchmarkSection Settings { get; set; } = new();

    public BenchmarkConfiguration Preserved { get; set; } = new();
}

[YamlObject]
public sealed partial class ProviderBenchmarkConfiguration
{
    public int Version { get; set; } = 1;

    public string Name { get; set; } = "Configuration.Writable provider benchmark";

    public List<BenchmarkSection> Sections { get; set; } = [];

    public static ProviderBenchmarkConfiguration Create(int itemCount) =>
        new()
        {
            Sections = Enumerable
                .Range(0, itemCount)
                .Select(index =>
                {
                    var section = BenchmarkSection.Create(4);
                    section.Name = $"section-{index:D4}";
                    return section;
                })
                .ToList(),
        };
}

[YamlObject]
public sealed partial class ProviderPartialConfigurationFile
{
    public BenchmarkSection Settings { get; set; } = new();

    public ProviderBenchmarkConfiguration Preserved { get; set; } = new();
}

public sealed record ProviderBenchmarkFormat(
    string AssemblyFileName,
    string ProviderTypeName,
    string FileExtension,
    string SectionName
)
{
    public static ProviderBenchmarkFormat Xml { get; } =
        new(
            "Configuration.Writable.Xml.dll",
            "Configuration.Writable.FormatProvider.XmlFormatProvider",
            "xml",
            "Settings"
        );

    public static ProviderBenchmarkFormat Yaml { get; } =
        new(
            "Configuration.Writable.Yaml.dll",
            "Configuration.Writable.FormatProvider.YamlFormatProvider",
            "yaml",
            "settings"
        );
}

internal static class ProviderBenchmarkData
{
    public static string Serialize(ProviderBenchmarkFormat format, object configuration) =>
        format == ProviderBenchmarkFormat.Xml
            ? SerializeXml(configuration)
            : SerializeYaml(configuration);

    private static string SerializeXml(object configuration)
    {
        var serializer = new XmlSerializer(
            configuration.GetType(),
            new XmlRootAttribute("configuration")
        );
        using var writer = new Utf8StringWriter();
        serializer.Serialize(writer, configuration);
        return writer.ToString();
    }

    private static string SerializeYaml(object configuration) =>
        configuration switch
        {
            ProviderBenchmarkConfiguration full => YamlSerializer.SerializeToString(full),
            ProviderPartialConfigurationFile partial => YamlSerializer.SerializeToString(partial),
            _ => throw new InvalidOperationException(
                $"Unexpected provider benchmark type '{configuration.GetType().FullName}'."
            ),
        };

    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}

internal static class ConfigurationChecksum
{
    public static int From(object configuration) =>
        configuration switch
        {
            BenchmarkConfiguration full => full.Version + full.Name.Length + full.Sections.Count,
            ProviderBenchmarkConfiguration full => full.Version
                + full.Name.Length
                + full.Sections.Count,
            BenchmarkSection section => section.Name.Length + section.Items.Count,
            _ => throw new InvalidOperationException(
                $"Unexpected configuration type '{configuration.GetType().FullName}'."
            ),
        };
}
