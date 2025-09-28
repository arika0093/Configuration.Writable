## WPF with Generic Host Example

1. Create a new WPF application project.
1. add `Microsoft.Extensions.Hosting` NuGet package to your project.
1. add `<EnableDefaultApplicationDefinition>false</EnableDefaultApplicationDefinition>` to your .csproj file to disable the default application definition.
1. remove `StartupUri="MainWindow.xaml"` from `App.xaml`.
1. add `Program.cs` file.

