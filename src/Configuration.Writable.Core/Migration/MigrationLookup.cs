#pragma warning disable S8969 // Dictionary TryGetValue out parameters require null-forgiving operators on supported target frameworks.
using System;
using System.Collections.Generic;

namespace Configuration.Writable.Migration;

/// <summary>
/// Precomputed lookups used while loading and applying configuration migrations.
/// </summary>
internal sealed class MigrationLookup
{
    private readonly Dictionary<Type, int?> _versionsByType = new();
    private readonly Dictionary<int, Type> _typesByVersion = new();
    private readonly Dictionary<Type, MigrationStep> _migrationsBySourceType = new();

    public MigrationLookup(
        Type targetType,
        OptionsSchemaMetadata targetMetadata,
        IReadOnlyList<MigrationStep> migrationSteps
    )
    {
        TargetVersion = targetMetadata.Version;
        AddType(targetType, targetMetadata.Version);

        foreach (var step in migrationSteps)
        {
            AddType(step.FromType, step.FromVersion);
            AddType(step.ToType, step.ToVersion);

            if (!_migrationsBySourceType.ContainsKey(step.FromType))
            {
                _migrationsBySourceType.Add(step.FromType, step);
            }
        }
    }

    public int? TargetVersion { get; }

    public bool TryGetType(int version, out Type type) =>
        _typesByVersion.TryGetValue(version, out type!);

    public bool TryGetMigration(Type sourceType, out MigrationStep migration) =>
        _migrationsBySourceType.TryGetValue(sourceType, out migration!);

    public int? GetVersion(Type type) => _versionsByType[type];

    private void AddType(Type type, int? version)
    {
        if (_versionsByType.ContainsKey(type))
        {
            return;
        }

        _versionsByType.Add(type, version);

        if (version is not null && !_typesByVersion.ContainsKey(version.Value))
        {
            _typesByVersion.Add(version.Value, type);
        }
    }
}
