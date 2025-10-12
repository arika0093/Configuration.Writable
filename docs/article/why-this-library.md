## Why This Library?
There are many ways to handle user settings in C# applications, but each has drawbacks and there is no de facto standard.

### Using `app.config` (`Settings.settings`)
This is a traditional approach and widely documented. However, you may encounter these issues:

* Manual editing of XML-based configuration files (or using Visual Studio's cumbersome GUI)
* Lack of type safety and unsuitability for complex settings
* Risk of settings being reset during updates if files are included in distributions without care

### Reading and Writing Configuration Files Yourself
For type safety, you might consider creating and managing your own configuration files.  
While this method works, it comes with many considerations:

* You must implement configuration management code yourself
* Features like backup creation and update handling require manual implementation
* Integrating multiple configuration sources takes extra effort
* You need to handle configuration change reflection yourself

### Using a Configuration Library
Given the boilerplate involved, you might look for a configuration library.  
A [NuGet search for `Config`](https://www.nuget.org/packages?q=config) yields many options.  
I reviewed several major libraries but found them unsuitable for these reasons:

* [DotNetConfig](https://github.com/dotnetconfig/dotnet-config)
  * Uses a proprietary file format (`.netconfig`)
  * Primarily designed for `dotnet tools`
* [Config.Net](https://github.com/aloneguid/config)
  * Supports various providers but uses a [unique storage format](https://github.com/aloneguid/config#flatline-syntax)
  * Collection writing is [not supported](https://github.com/aloneguid/config#json) in the JSON provider

### Extending `Microsoft.Extensions.Configuration`
`Microsoft.Extensions.Configuration` (MS.E.C) is the most standardized configuration management method today.  
It offers features like multi-file integration, support for various formats (including environment variables), configuration change reflection, and seamless integration with `IHostApplicationBuilder`.  
However, it's mainly designed for application settings and lacks support for writing user settings.

Since saving configuration isn't supported, extending MS.E.C seems natural.  
Automatic file updates can be handled with `IOptionsMonitor`, so adding save functionality appears sufficient.  
I began developing the library with this in mind (and found a few similar libraries).  
However, I encountered several issues:

* MS.E.C is specialized for loading configuration at startup, making it hard to add or remove files later
* With multiple configuration files, you can only access the merged result—not individual files for reference or saving
* It relies on DI, which is cumbersome for applications that don't use DI
  * Holding an internal `ServiceProvider` can help, but it's unintuitive and adds dependencies
* Provider construction is awkward
  * Reading uses MS.E.C providers, but writing requires custom implementations, which is not intuitive
  * Extension and testing become difficult

Solving these problems would require major changes to MS.E.C, diminishing its benefits.

### Extending `Microsoft.Extensions.Options`
Instead of extending MS.E.C, I chose to extend various options in `Microsoft.Extensions.Options` (MS.E.O).  
That is the basis of this library (Configuration.Writable).
Although we call it an "extension," we decided to rebuild the interface from the ground up to solve issues such as integration with setting persistence.

As a result, this library provides configuration management similar to MS.E.C, with save functionality and small dependencies.  
It only depends on the following packages:

* `Microsoft.Extensions.Options` (for the Options pattern)
  * `Microsoft.Extensions.Primitives` (dependency of MS.E.O)
* `Microsoft.Extensions.Logging.Abstractions` (for logging)
* `Microsoft.Extensions.DependencyInjection.Abstractions` (for DI integration)

Since it doesn't require DI like MS.E.C, it's easy to use in applications that don't use DI.

Give it a try!