---
sidebar_position: 4
---

# Dynamic Add/Remove Options

Configuration.Writable supports dynamically adding and removing configuration instances at runtime using `IWritableOptionsConfigRegistry`.

## Overview

While most applications configure options at startup, some scenarios require dynamic configuration management:

- User opens/closes documents with individual settings
- Multi-tenant applications with per-tenant configurations
- Plugin systems where plugins have their own settings
- Dynamic environment switching

## Basic Usage

### Registry Interface

Use `IWritableOptionsConfigRegistry<T>` to manage configurations dynamically:

```csharp
public class DynamicOptionsService(
    IWritableOptionsConfigRegistry<UserSetting> registry,
    IWritableNamedOptions<UserSetting> options
)
{
    public void AddNewOptions(string instanceName, string filePath)
    {
        // Dynamically add a new configuration instance
        var success = registry.TryAdd(instanceName, conf =>
        {
            conf.UseFile(filePath);
            conf.SectionName = instanceName;
        });
        
        if (success)
        {
            Console.WriteLine($"Added instance: {instanceName}");
        }
    }
    
    public void RemoveOptions(string instanceName)
    {
        // Remove a configuration instance
        var success = registry.TryRemove(instanceName);
        
        if (success)
        {
            Console.WriteLine($"Removed instance: {instanceName}");
        }
    }
    
    public async Task UseOptions(string instanceName)
    {
        // Access the dynamically added instance
        var settings = options.Get(instanceName);
        Console.WriteLine($"Settings for {instanceName}: {settings.Name}");
        
        // Modify and save
        await options.SaveAsync(instanceName, s =>
        {
            s.Name = "Updated via dynamic instance";
        });
    }
}
```

## Complete Example: Document Manager

Here's a complete example of a document manager that maintains settings for each open document:

```csharp
using Configuration.Writable;

var builder = Host.CreateApplicationBuilder(args);

// Register common application settings (static)
builder.Services.AddWritableOptions<DocumentSettings>("Common", conf =>
{
    conf.UseFile("common-settings.json");
});

// Register the document manager
builder.Services.AddSingleton<DocumentManager>();

var app = builder.Build();

// Simulate document operations
var docManager = app.Services.GetRequiredService<DocumentManager>();
await docManager.OpenDocumentAsync("Document1", "/path/to/doc1.json");
await docManager.OpenDocumentAsync("Document2", "/path/to/doc2.json");
await docManager.UseDocumentAsync("Document1");
await docManager.CloseDocumentAsync("Document1");

app.Run();

// Settings class
public partial class DocumentSettings : IOptionsModel<DocumentSettings>
{
    public string Name { get; set; } = "Untitled";
    public string LastEditedBy { get; set; } = "Unknown";
    public DateTime LastModified { get; set; } = DateTime.Now;
}

// Document manager service
public class DocumentManager
{
    private readonly IWritableOptionsConfigRegistry<DocumentSettings> _registry;
    private readonly IWritableNamedOptions<DocumentSettings> _options;
    private readonly HashSet<string> _openDocuments = new();
    
    public DocumentManager(
        IWritableOptionsConfigRegistry<DocumentSettings> registry,
        IWritableNamedOptions<DocumentSettings> options)
    {
        _registry = registry;
        _options = options;
    }
    
    public async Task OpenDocumentAsync(string documentName, string settingsPath)
    {
        if (_openDocuments.Contains(documentName))
        {
            Console.WriteLine($"Document {documentName} is already open");
            return;
        }
        
        // Dynamically add settings for this document
        var success = _registry.TryAdd(documentName, conf =>
        {
            conf.UseFile(settingsPath);
            conf.SectionName = documentName;
        });
        
        if (success)
        {
            _openDocuments.Add(documentName);
            Console.WriteLine($"Opened document: {documentName}");
            
            var settings = _options.Get(documentName);
            Console.WriteLine($"  Name: {settings.Name}");
            Console.WriteLine($"  Last edited: {settings.LastModified}");
        }
    }
    
    public async Task UseDocumentAsync(string documentName)
    {
        if (!_openDocuments.Contains(documentName))
        {
            Console.WriteLine($"Document {documentName} is not open");
            return;
        }
        
        // Get and update document settings
        await _options.SaveAsync(documentName, settings =>
        {
            settings.LastEditedBy = "CurrentUser";
            settings.LastModified = DateTime.Now;
        });
        
        Console.WriteLine($"Updated document: {documentName}");
    }
    
    public async Task CloseDocumentAsync(string documentName)
    {
        if (!_openDocuments.Contains(documentName))
        {
            Console.WriteLine($"Document {documentName} is not open");
            return;
        }
        
        // Remove the dynamic configuration
        var success = _registry.TryRemove(documentName);
        
        if (success)
        {
            _openDocuments.Remove(documentName);
            Console.WriteLine($"Closed document: {documentName}");
        }
    }
    
    public void ListOpenDocuments()
    {
        Console.WriteLine("Open documents:");
        foreach (var doc in _openDocuments)
        {
            var settings = _options.Get(doc);
            Console.WriteLine($"  - {doc}: {settings.Name}");
        }
    }
}
```

## Multi-Tenant Example

Dynamic configuration for multi-tenant applications:

```csharp
public class TenantConfigurationManager
{
    private readonly IWritableOptionsConfigRegistry<TenantSettings> _registry;
    private readonly IWritableNamedOptions<TenantSettings> _options;
    
    public TenantConfigurationManager(
        IWritableOptionsConfigRegistry<TenantSettings> registry,
        IWritableNamedOptions<TenantSettings> options)
    {
        _registry = registry;
        _options = options;
    }
    
    public void ActivateTenant(string tenantId)
    {
        _registry.TryAdd(tenantId, conf =>
        {
            conf.UseStandardSaveDirectory("MyApp")
                .AddFilePath($"tenants/{tenantId}/settings.json");
            conf.SectionName = "TenantSettings";
        });
    }
    
    public void DeactivateTenant(string tenantId)
    {
        _registry.TryRemove(tenantId);
    }
    
    public async Task ConfigureTenantAsync(string tenantId, Action<TenantSettings> configure)
    {
        await _options.SaveAsync(tenantId, configure);
    }
}

public partial class TenantSettings : IOptionsModel<TenantSettings>
{
    public string TenantName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public Dictionary<string, string> Features { get; set; } = new();
}
```

## Important Notes

### Registry Behavior

- `TryAdd`: Returns `true` if instance was added, `false` if name already exists
- `TryRemove`: Returns `true` if instance was removed, `false` if name doesn't exist
- Both operations are thread-safe

### Limitations

:::warning
- Dynamic instances are **not persisted** across application restarts
- You need to re-add instances on application startup
- Removing an instance doesn't delete the configuration file
:::

### Cleanup

Always remove instances when they're no longer needed:

```csharp
public class DocumentService : IDisposable
{
    private readonly List<string> _managedInstances = new();
    private readonly IWritableOptionsConfigRegistry<DocumentSettings> _registry;
    
    public void AddInstance(string name)
    {
        _registry.TryAdd(name, conf => { });
        _managedInstances.Add(name);
    }
    
    public void Dispose()
    {
        // Cleanup all managed instances
        foreach (var instance in _managedInstances)
        {
            _registry.TryRemove(instance);
        }
    }
}
```

## Change Detection with Dynamic Instances

Monitor changes to dynamically added instances:

```csharp
public class DynamicMonitor(IWritableNamedOptions<UserSetting> options)
{
    private readonly Dictionary<string, IDisposable> _watchers = new();
    
    public void StartWatching(string instanceName)
    {
        var watcher = options.OnChange(instanceName, settings =>
        {
            Console.WriteLine($"{instanceName} changed: {settings.Name}");
        });
        
        _watchers[instanceName] = watcher;
    }
    
    public void StopWatching(string instanceName)
    {
        if (_watchers.TryGetValue(instanceName, out var watcher))
        {
            watcher.Dispose();
            _watchers.Remove(instanceName);
        }
    }
}
```

## Use Cases

Dynamic configuration is ideal for:

- **Document/File Editors**: Per-document settings
- **Multi-Tenant SaaS**: Per-tenant configurations
- **Plugin Systems**: Plugin-specific settings
- **Dynamic Environments**: Runtime environment switching
- **Session-Based Config**: Per-session or per-connection settings

## Best Practices

1. **Track Instances**: Keep a list of dynamically added instances
2. **Cleanup**: Always remove instances when done
3. **Error Handling**: Check return values of `TryAdd`/`TryRemove`
4. **Thread Safety**: Registry operations are thread-safe, but manage your state carefully
5. **Persist State**: Store which instances should be loaded on startup

## Next Steps

- [InstanceName](./instance-name) - Static multiple instances
- [Change Detection](../customization/change-detection) - Monitor changes
- [Testing](./testing) - Test dynamic scenarios
