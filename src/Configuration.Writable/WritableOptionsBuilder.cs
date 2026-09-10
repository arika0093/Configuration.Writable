using System;
using System.Collections.Generic;
using Configuration.Writable.Configure;
using Microsoft.Extensions.DependencyInjection;
using MEOptions = Microsoft.Extensions.Options.Options;

namespace Configuration.Writable;

/// <summary>Collects shared configuration and writable options registrations.</summary>
public class WritableOptionsBuilder : WritableOptionsConfigBuilder
{
    private readonly List<Action> registrations = [];
    private readonly HashSet<Type> registeredTypes = [];

    internal void AddDeferred(Action registration) => registrations.Add(registration);

    /// <summary>Adds an options type with the default instance name.</summary>
    public void Add<T>()
        where T : class, new() => Add<T>(MEOptions.DefaultName, _ => { });

    /// <summary>Adds and configures an options type.</summary>
    public void Add<T>(Action<WritableOptionsConfigBuilder<T>> configure)
        where T : class, new() => Add(MEOptions.DefaultName, configure);

    /// <summary>Adds a named options type.</summary>
    public void Add<T>(string instanceName)
        where T : class, new() => Add<T>(instanceName, _ => { });

    /// <summary>Adds and configures a named options type.</summary>
    public void Add<T>(string instanceName, Action<WritableOptionsConfigBuilder<T>> configure)
        where T : class, new() => AddDeferred(() => Register(instanceName, configure));

    internal void Execute()
    {
        foreach (var registration in registrations)
            registration();
        JsonSchemaGeneration.GenerateIfRequested(
            JsonSchemaGenerationEnabled,
            JsonSchemaTypeInfoResolver,
            SchemaBaseUri
        );
    }

    /// <summary>Processes a collected registration.</summary>
    protected virtual void Register<T>(
        string instanceName,
        Action<WritableOptionsConfigBuilder<T>> configure
    )
        where T : class, new()
    {
        var builder = new WritableOptionsConfigBuilder<T>(this);
        configure(builder);
        var replace = registeredTypes.Add(typeof(T));
        WritableOptions.InitializeInternal(instanceName, builder, replace);
    }
}

/// <summary>Collects shared configuration and dependency-injection registrations.</summary>
public class WritableOptionsServiceBuilder : WritableOptionsBuilder
{
    private readonly IServiceCollection services;

    /// <summary>Creates a builder for a service collection.</summary>
    public WritableOptionsServiceBuilder(IServiceCollection services) => this.services = services;

    /// <summary>Adds a profiled options type.</summary>
    public void AddProfiled<T>(Action<ProfiledOptionsConfigBuilder<T>> configure)
        where T : class, new() =>
        AddDeferred(() =>
        {
            var builder = new ProfiledOptionsConfigBuilder<T>();
            builder.CopyFrom(this);
            configure(builder);
            builder.RegisterAsSingleton = false;
            services.AddProfiledWritableOptions(builder);
        });

    /// <inheritdoc />
    protected override void Register<T>(
        string instanceName,
        Action<WritableOptionsConfigBuilder<T>> configure
    )
    {
        var builder = new WritableOptionsConfigBuilder<T>(this);
        configure(builder);
        services.AddWritableOptions(instanceName, builder);
    }
}
