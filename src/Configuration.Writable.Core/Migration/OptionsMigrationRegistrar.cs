using System;
using System.Collections.Generic;
using System.Linq;

namespace Configuration.Writable.Migration;

internal sealed class OptionsMigrationRegistrar(List<MigrationStep> steps)
    : IOptionsMigrationRegistrar
{
    public void Register<TOld, TNew>(
        Func<TOld, TNew> migrator,
        string modelId,
        int oldVersion,
        int newVersion
    )
        where TOld : class, new()
        where TNew : class, new()
    {
        if (steps.Any(step => step.FromType == typeof(TOld) && step.ToType == typeof(TNew)))
        {
            return;
        }

        ValidateVersions(typeof(TOld), typeof(TNew), oldVersion, newVersion);
        steps.Add(new MigrationStep<TOld, TNew>(migrator, modelId, oldVersion, newVersion));
    }

    internal static void ValidateVersions(
        Type oldType,
        Type newType,
        int oldVersion,
        int newVersion
    )
    {
        if (oldVersion <= 0 || newVersion <= oldVersion)
        {
            throw new InvalidOperationException(
                $"Migration downgrade detected: Cannot migrate from version {oldVersion} ({oldType.Name}) to version {newVersion} ({newType.Name})."
            );
        }
    }
}
