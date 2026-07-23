# Configuration.Writable benchmarks

Run from the repository root:

```bash
dotnet run -c Release --project benchmarks/Configuration.Writable.Benchmarks -- --filter '*'
```

The project compares local source assemblies with the published `0.5.0` `Configuration.Writable`, `Configuration.Writable.Xml`, and `Configuration.Writable.Yaml` packages. The two implementations load in separate `AssemblyLoadContext` instances so their identical public type names do not conflict. The published package is the BenchmarkDotNet baseline in every benchmark class.

XML and YAML benchmarks cover full save, full load, and partial save at 4, 64, and 512 items. Their local provider assemblies and dependencies are copied to the benchmark output's `local` directory at build time.
