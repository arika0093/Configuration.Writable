## Why This Library?
There are many ways to handle user settings in C# applications. However, each has some drawbacks, and no de facto standard exists.

### Using `app.config` (`Settings.settings`)
This is an old-fashioned approach that yields many search results. When you start using it, you'll likely encounter the following issues:

* You need to manually write XML-based configuration files (or use Visual Studio's cumbersome GUI)
* It lacks type safety and is unsuitable for complex settings
* Files may be included in distributions without careful consideration, risking settings reset during updates

### Reading and Writing Configuration Files Yourself
When considering type safety, the first approach that comes to mind is creating and reading/writing your own configuration files.  
This method isn't bad, but the drawback is that there are too many things to consider.

* You need to write configuration management code yourself
* You need to implement many features like backup creation, update handling
* Integrating multiple configuration sources requires extra effort
* You need to implement configuration change reflection yourself

### Using (Any Configuration Library)
Since there's so much boilerplate code, there must be some configuration library available.  
Indeed, just [searching for `Config` on NuGet](https://www.nuget.org/packages?q=config) yields many libraries.  
I examined the major ones among these, but couldn't adopt them for the following reasons:

* [DotNetConfig](https://github.com/dotnetconfig/dotnet-config)
  * The file format uses a proprietary format (`.netconfig`)
  * It appears to be primarily designed for `dotnet tools`
* [Config.Net](https://github.com/aloneguid/config)
  * It supports various providers but uses a [unique storage format](https://github.com/aloneguid/config#flatline-syntax)
  * Collection writing is [not supported](https://github.com/aloneguid/config#json) in the JSON provider

### Using `Microsoft.Extensions.Configuration`
`Microsoft.Extensions.Configuration` (`MS.E.C`) can be said to be the most standardized configuration management method in modern times.  
It provides many features such as multi-file integration, support for various formats including environment variables, and configuration change reflection, and integrates seamlessly with `IHostApplicationBuilder`.  
However, since it's primarily designed for application settings, it's insufficient for handling user settings. The major problem is that configuration writing is not supported.  
Another issue is that, being based on DI (Dependency Injection), it can be somewhat cumbersome to use in certain types of applications.
For example, applications like `WinForms`, `WPF`, or `Console Apps` that want to use configuration files are less likely to utilize DI.

### `Configuration.Writable`
The preamble has gotten long, but it's time for promotion!  
This library extends `MS.E.C` to make writing user settings easy.  The name of this library is `Configuration.Writable` because it adds the "writable" feature.
It's also designed to be easily usable in applications that don't use DI.  
